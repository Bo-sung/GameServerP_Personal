# FPS/RTS 하이브리드 동기화 전략

## 1. 동기화 계층

```
계층 1: FPS 플레이어 (높은 주기 - 60Hz)
  ├─ 위치 보간
  ├─ 클라이언트 예측
  └─ 서버 재조정

계층 2: AI 봇 (중간 주기 - 30Hz)
  ├─ 상태 벡터 동기화
  └─ 액션 명령 전달

계층 3: RTS 커맨더 (낮은 주기 - 10Hz)
  ├─ 유닛 상태 개요
  └─ 게임 이벤트 알림
```

---

## 2. 클라이언트 예측 (FPS)

### 2.1 예측 알고리즘

```csharp
public class PlayerPrediction {
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public Quaternion Rotation { get; set; }
    
    // 클라이언트 측 예측
    public void PredictMovement(float deltaTime) {
        // 입력 기반 위치 계산
        Vector3 acceleration = CalculateAcceleration(currentInput);
        Velocity += acceleration * deltaTime;
        Position += Velocity * deltaTime;
        
        // 마찰 적용
        Velocity *= 0.98f;
    }
    
    // 서버에서 수신한 실제 위치로 조정
    public void ReconcileWithServer(Vector3 serverPosition, Vector3 serverVelocity) {
        // 오차 계산
        Vector3 error = serverPosition - Position;
        float errorMagnitude = error.magnitude;
        
        if (errorMagnitude > 1.0f) {
            // 큰 오차: 순간 이동 (텔레포트)
            Position = serverPosition;
            Velocity = serverVelocity;
        } else if (errorMagnitude > 0.1f) {
            // 작은 오차: 부드러운 조정
            Position = Vector3.Lerp(Position, serverPosition, 0.5f);
            Velocity = Vector3.Lerp(Velocity, serverVelocity, 0.3f);
        }
        // 오차 < 0.1f: 무시 (정상 범위)
    }
}
```

### 2.2 예측 오차 처리

```
예측 오차가 발생하는 이유:

1. 네트워크 지연 (50~100ms)
   ├─ 클라이언트가 미래를 예측
   └─ 서버가 과거 상태를 전송

2. 입력 해석 차이
   ├─ 클라이언트: 로컬 입력 해석
   └─ 서버: 네트워크 패킷 기반 해석

3. 물리 엔진 차이
   ├─ 부동소수점 오차
   └─ 타이밍 차이

해결:
→ 1틱마다 서버 상태 수신
→ 보간으로 부드러운 이동
→ 임계값 기반 조정
```

---

## 3. 라그 보상 (Lag Compensation)

### 3.1 히트스캔 총기 시스템

```csharp
public class HitscanSystem {
    public void FireWeapon(Vector3 firePosition, Vector3 fireDirection) {
        // 1단계: 현재 상태로 히트 판정
        RaycastHit hit;
        if (Physics.Raycast(firePosition, fireDirection, out hit)) {
            DamageAgent(hit.collider.gameObject, hit.point);
            return;
        }
        
        // 2단계: 클라이언트 RTT 기반 역추적
        float rttSeconds = GetRoundTripTime() / 1000f;
        
        // 각 AI 봇의 과거 위치 검사
        for (int i = 0; i < activeBots.Count; i++) {
            Bot bot = activeBots[i];
            Vector3 pastPosition = bot.GetPositionAtTime(
                Time.time - rttSeconds
            );
            
            // 과거 위치로 재판정
            if (IsInRaycast(firePosition, fireDirection, pastPosition)) {
                DamageAgent(bot.gameObject, pastPosition);
                return;
            }
        }
    }
    
    private bool IsInRaycast(Vector3 rayStart, Vector3 rayDir, Vector3 point) {
        Vector3 toPoint = point - rayStart;
        float distOnRay = Vector3.Dot(toPoint, rayDir.normalized);
        
        if (distOnRay < 0) return false;  // 뒤쪽
        
        Vector3 perpendicular = toPoint - (rayDir.normalized * distOnRay);
        return perpendicular.magnitude < 0.5f;  // 충돌 반경
    }
}
```

### 3.2 위치 이력 저장

```csharp
public class PositionHistory {
    private Queue<PositionSnapshot> history = new();
    private const int MaxSnapshots = 60;  // 1초 (60Hz)
    
    public struct PositionSnapshot {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;
    }
    
    public void RecordPosition(Vector3 pos, Vector3 vel) {
        history.Enqueue(new PositionSnapshot {
            timestamp = Time.time,
            position = pos,
            velocity = vel
        });
        
        if (history.Count > MaxSnapshots) {
            history.Dequeue();  // 오래된 데이터 제거
        }
    }
    
    public Vector3 GetPositionAtTime(float targetTime) {
        PositionSnapshot? before = null;
        PositionSnapshot? after = null;
        
        foreach (var snap in history) {
            if (snap.timestamp <= targetTime) before = snap;
            if (snap.timestamp >= targetTime) {
                after = snap;
                break;
            }
        }
        
        if (before == null) return history.Peek().position;
        if (after == null) return history.Last().position;
        
        // 보간
        float alpha = (targetTime - before.Value.timestamp) / 
                      (after.Value.timestamp - before.Value.timestamp);
        return Vector3.Lerp(before.Value.position, after.Value.position, alpha);
    }
}
```

---

## 4. AI 봇 상태 동기화

### 4.1 상태 벡터 동기화

```
Unreal 서버에서 C# Bot Controller로:
  ┌────────────────────────────┐
  │ BotState (30Hz)            │
  ├────────────────────────────┤
  │ ID, Position, Health,      │
  │ Suppression, State, InCover│
  └────────────────────────────┘
        ↓ (네트워크)
  ┌────────────────────────────┐
  │ C# Bot Agent 업데이트      │
  │ - Position 기록            │
  │ - Health 변경 감지         │
  │ - State 전환 판정          │
  └────────────────────────────┘
        ↓ (BotInput 생성)
  ┌────────────────────────────┐
  │ Unreal PlayerController    │
  │ - 이동 명령 실행           │
  │ - 공격 실행                │
  │ - 상태 업데이트            │
  └────────────────────────────┘
```

### 4.2 상태 검증

```csharp
public class BotStateSynchronization {
    private const float MaxPositionDelta = 50f;  // 1틱 안에 변할 수 있는 최대 거리
    private const float UpdateInterval = 1f / 30f;  // 33ms
    
    public bool ValidateStateUpdate(BotStatePacket received, BotAgent local) {
        // 1. 위치 검증
        float posDelta = Vector3.Distance(received.Position, local.Position);
        if (posDelta > MaxPositionDelta) {
            Debug.LogWarning($"Bot {received.ID}: Teleport detected {posDelta}m");
            // 네트워크 끊김 가능성 → 천천히 조정
            local.Position = Vector3.Lerp(local.Position, received.Position, 0.3f);
            return false;
        }
        
        // 2. 체력 검증
        if (received.Health > local.Health + 50) {
            Debug.LogError($"Bot {received.ID}: Health increased {local.Health} → {received.Health}");
            return false;
        }
        
        // 3. 상태 검증
        if (!IsValidStateTransition(local.State, (BotState)received.State)) {
            Debug.LogWarning($"Bot {received.ID}: Invalid state {local.State} → {received.State}");
            return false;
        }
        
        return true;
    }
    
    private bool IsValidStateTransition(BotState from, BotState to) {
        // 상태 전환 규칙
        return (from, to) switch {
            (BotState.Patrol, BotState.Investigate) => true,
            (BotState.Patrol, BotState.Combat) => true,
            (BotState.Investigate, BotState.Combat) => true,
            (BotState.Investigate, BotState.Patrol) => true,
            (BotState.Combat, BotState.TacticalRetreat) => true,
            (BotState.Combat, BotState.Patrol) => true,
            (BotState.TacticalRetreat, BotState.Combat) => true,
            (BotState.TacticalRetreat, BotState.Healing) => true,
            (BotState.Healing, BotState.Patrol) => true,
            (BotState.Healing, BotState.Combat) => true,
            _ => false
        };
    }
}
```

---

## 5. RTS 클라이언트 동기화

### 5.1 매 10Hz 업데이트 처리

```csharp
public class RTSGameStateManager {
    private Dictionary<int, BotVisualState> visualStates = new();
    
    public void HandleUnitStatusUpdate(UnitStatusUpdatePacket packet) {
        foreach (var unitData in packet.Units) {
            int botID = unitData.BotID;
            
            if (!visualStates.ContainsKey(botID)) {
                // 새 유닛 생성
                BotVisualState visual = CreateUnitVisual(botID);
                visualStates[botID] = visual;
            }
            
            BotVisualState state = visualStates[botID];
            
            // 화면 업데이트 (보간됨)
            state.SetTargetPosition(unitData.Position);
            state.SetHealth(unitData.Health);
            state.SetState((BotState)unitData.State);
        }
    }
    
    public void UpdateVisuals() {
        foreach (var kvp in visualStates) {
            kvp.Value.UpdateVisualPosition();  // 보간
        }
    }
}

public class BotVisualState {
    private Vector3 currentPosition;
    private Vector3 targetPosition;
    private float lerpSpeed = 2f;  // 보간 속도
    
    public void SetTargetPosition(Vector3 target) {
        targetPosition = target;
    }
    
    public void UpdateVisualPosition() {
        currentPosition = Vector3.Lerp(currentPosition, targetPosition, 
                                      lerpSpeed * Time.deltaTime);
        
        // GameObject 업데이트
        gameObject.transform.position = currentPosition;
    }
}
```

### 5.2 명령 신뢰성 보장

```csharp
public class RTSCommandHandler {
    private Dictionary<int, RTSCommand> pendingCommands = new();
    private int commandSequence = 0;
    
    public void SendCommand(RTSCommand command) {
        int cmdSeq = commandSequence++;
        command.Sequence = cmdSeq;
        
        // 로컬 즉시 처리
        ApplyCommandLocally(command);
        
        // 서버로 전송 + 확인 대기
        SendToServer(command);
        pendingCommands[cmdSeq] = command;
        
        // 타임아웃 설정 (2초)
        StartCoroutine(WaitForAck(cmdSeq, 2f));
    }
    
    private IEnumerator WaitForAck(int sequence, float timeout) {
        float elapsed = 0;
        while (elapsed < timeout && pendingCommands.ContainsKey(sequence)) {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (pendingCommands.ContainsKey(sequence)) {
            // 타임아웃: 재전송
            RTSCommand cmd = pendingCommands[sequence];
            Debug.LogWarning($"Command {sequence} timeout, retrying...");
            SendToServer(cmd);
            StartCoroutine(WaitForAck(sequence, 2f));
        }
    }
    
    public void HandleCommandAck(CommandAckPacket ack) {
        if (pendingCommands.ContainsKey(ack.Sequence)) {
            pendingCommands.Remove(ack.Sequence);
            if (!ack.Success) {
                Debug.LogWarning($"Command {ack.Sequence} failed");
            }
        }
    }
}
```

---

## 6. 재연결 처리

### 6.1 상태 복구

```csharp
public class ReconnectionHandler {
    public async Task<bool> ReconnectAsync() {
        try {
            // 1. 서버 재연결
            bool connected = await ConnectToServerAsync();
            if (!connected) return false;
            
            // 2. 현재 게임 상태 요청
            GameStateSnapshot snapshot = await RequestGameStateAsync();
            
            // 3. 로컬 상태 업데이트
            ApplyGameStateSnapshot(snapshot);
            
            // 4. 동기화 확인
            await SynchronizeAsync();
            
            return true;
        } catch (Exception ex) {
            Debug.LogError($"Reconnection failed: {ex.Message}");
            return false;
        }
    }
    
    private GameStateSnapshot ApplyGameStateSnapshot(GameStateSnapshot snapshot) {
        // 현재 위치 설정
        Player.Position = snapshot.PlayerPosition;
        Player.Health = snapshot.PlayerHealth;
        
        // 모든 봇 위치 재설정
        foreach (var botState in snapshot.BotStates) {
            Bot bot = GetBot(botState.ID);
            bot.Position = botState.Position;
            bot.Health = botState.Health;
            bot.State = botState.State;
        }
        
        // 게임 상태 복구
        GameTimer.CurrentTime = snapshot.GameTime;
        ScoreBoard.UpdateScores(snapshot.Scores);
    }
}
```

### 6.2 타임아웃 정책

```
네트워크 상태 모니터링:

Good (<50ms):
  ├─ 업데이트: 30Hz
  └─ 재시도: 2회

Fair (50-100ms):
  ├─ 업데이트: 20Hz (네트워크 부하 감소)
  └─ 재시도: 3회

Poor (>100ms):
  ├─ 업데이트: 10Hz
  ├─ 재시도: 5회
  └─ 경고: 플레이어에게 알림
```

---

## 7. 이벤트 기반 동기화

### 7.1 중요 이벤트

```csharp
public class GameEventSynchronization {
    public enum GameEvent {
        BotKilled = 0x01,
        PlayerKilled = 0x02,
        GameStarted = 0x03,
        GameEnded = 0x04,
        BotSpawned = 0x05,
        CoverDestroyed = 0x06
    }
    
    public void BroadcastEvent(GameEvent evt, Dictionary<string, object> data) {
        var packet = new EventPacket {
            EventType = evt,
            Timestamp = Time.time,
            Data = Serialize(data)
        };
        
        // TCP (신뢰도 필수)
        SendReliable(packet);
        
        // 모든 클라이언트에 브로드캐스트
        BroadcastToAllClients(packet);
    }
}
```

### 7.2 중복 이벤트 제거

```csharp
public class EventDeduplicator {
    private Dictionary<ulong, float> recentEvents = new();
    
    public bool ShouldProcess(ulong eventHash) {
        float currentTime = Time.time;
        
        if (recentEvents.ContainsKey(eventHash)) {
            float lastTime = recentEvents[eventHash];
            if (currentTime - lastTime < 0.1f) {
                return false;  // 중복 (100ms 이내)
            }
        }
        
        recentEvents[eventHash] = currentTime;
        return true;
    }
}
```

---

## 8. 성능 최적화

### 8.1 거리 기반 업데이트

```csharp
public class InterestManagement {
    private const float CullDistance = 100f;  // 100m 이상 거리 봇은 업데이트 감소
    
    public void UpdateBotUpdateFrequency(Bot bot, Vector3 playerPos) {
        float distance = Vector3.Distance(bot.Position, playerPos);
        
        if (distance < 30f) {
            // 매우 가까움: 60Hz 업데이트
            bot.UpdateFrequency = 60;
        } else if (distance < 60f) {
            // 가까움: 30Hz 업데이트
            bot.UpdateFrequency = 30;
        } else if (distance < CullDistance) {
            // 중간: 10Hz 업데이트
            bot.UpdateFrequency = 10;
        } else {
            // 매우 멀음: 업데이트 생략
            bot.UpdateFrequency = 0;
        }
    }
}
```

### 8.2 대역폭 절감

```
최적화 기법:

1. Delta Compression
   ├─ 변경된 필드만 전송
   └─ 약 40% 대역폭 절감

2. Update Culling
   ├─ 거리 기반 업데이트 빈도
   └─ 약 30% 절감

3. Quantization
   ├─ Float → Int 압축
   └─ 약 25% 절감

총합: ~65% 대역폭 절감 가능
```

---

## 9. 동기화 체크리스트

```
✓ 클라이언트 예측 구현
✓ 라그 보상 시스템
✓ 상태 검증
✓ 재연결 처리
✓ 이벤트 중복 제거
✓ 거리 기반 업데이트
✓ 타임아웃 처리
✓ 모니터링 로깅
```

