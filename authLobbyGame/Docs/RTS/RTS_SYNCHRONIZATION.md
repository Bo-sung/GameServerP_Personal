# RTS 동기화 전략 (RTS Synchronization Strategy)

## 1. 동기화 원칙

### 1.1 기본 개념
```
모든 RTS 서버는 다음을 보장해야 함:

1. 일관성 (Consistency):
   - 모든 클라이언트의 게임 상태 일치
   - 서버가 진실의 원천 (Authority)

2. 응답성 (Responsiveness):
   - 플레이어 명령에 대한 빠른 피드백
   - 클라이언트 로컬 예측으로 지연 숨김

3. 확장성 (Scalability):
   - 많은 유닛 처리 효율화
   - 네트워크 대역폭 최소화

4. 공정성 (Fairness):
   - 플레이어 간 지연 시간 평형
   - 순환 버퍼 (Circular Buffer) 사용
```

### 1.2 동기화 모델

```
┌─────────────────────────────────────────────┐
│          게임 루프 (100ms 틱)               │
├─────────────────────────────────────────────┤
│  T0: 명령 수집                              │
│  T1: 유효성 검사                            │
│  T2: 게임 로직 실행                         │
│  T3: 동기화 패킷 생성                       │
│  T4: 클라이언트로 전송                      │
└─────────────────────────────────────────────┘

네트워크 지연 (RTT): 50~250ms
동기화 간격: 100ms (10 ticks/sec)
```

---

## 2. 명령 처리 (Command Processing)

### 2.1 명령 워크플로우
```
플레이어 입력 → 로컬 예측 → 서버 전송 → 
서버 검증 → 서버 실행 → 동기화 전송 → 
클라이언트 보정
```

### 2.2 명령 우선순위
```csharp
public class CommandPriority
{
    // 우선순위 (낮을수록 먼저 실행)
    
    // 1. 긴급 명령 (Priority: 0)
    - StopCommand
    - HoldPositionCommand
    - CancelConstruction
    
    // 2. 이동 명령 (Priority: 10)
    - MoveCommand
    - PatrolCommand
    
    // 3. 공격 명령 (Priority: 20)
    - AttackCommand
    - AttackGroundCommand
    
    // 4. 작업 명령 (Priority: 30)
    - GatherCommand
    - BuildCommand
    - TrainCommand
    
    // 5. 기타 명령 (Priority: 40)
    - ResearchCommand
    - UpgradeCommand
}
```

### 2.3 명령 시퀀싱
```csharp
public class CommandSequencer
{
    private Dictionary<int, Queue<GameCommand>> playerCommandQueues;
    private Dictionary<int, long> lastProcessedSequence;
    
    public void ProcessCommands(float deltaTime)
    {
        foreach (var playerId in playerCommandQueues.Keys)
        {
            var commandQueue = playerCommandQueues[playerId];
            var lastSeq = lastProcessedSequence[playerId];
            
            while (commandQueue.Count > 0)
            {
                var command = commandQueue.Peek();
                
                // 시퀀스 번호 순서 확인
                if (command.SequenceNumber <= lastSeq)
                {
                    commandQueue.Dequeue(); // 중복 제거
                    continue;
                }
                
                if (!command.Validate(gameState))
                {
                    commandQueue.Dequeue(); // 유효하지 않은 명령 제거
                    SendCommandError(playerId, command);
                    continue;
                }
                
                // 명령 실행
                command.Execute(gameState);
                commandQueue.Dequeue();
                lastProcessedSequence[playerId] = command.SequenceNumber;
            }
        }
    }
}
```

### 2.4 지연 보정 (Latency Compensation)
```csharp
public class LatencyCompensation
{
    private const int TICK_RATE = 100; // ms
    private Dictionary<int, int> playerLatencyMs;
    
    public void AdjustCommandExecution(GameCommand command)
    {
        int playerLatency = playerLatencyMs[command.PlayerId];
        int ticsToCompensate = Mathf.RoundToInt(playerLatency / TICK_RATE);
        
        // 클라이언트에서 보낸 시점: T
        // 서버 수신 시점: T + latency
        // 실행 시점: T + latency + 1 tick (확실성을 위해)
        
        // 예: RTT = 100ms (50ms latency)
        // 클라이언트 T0에서 발송 → 서버 T0.05에서 수신 → T0.1에서 실행
        
        command.ExecutionTick = gameState.CurrentTick + ticsToCompensate + 1;
    }
    
    public void UpdatePlayerLatency(int playerId, int latencyMs)
    {
        playerLatencyMs[playerId] = latencyMs;
    }
}
```

---

## 3. 클라이언트 로컬 예측 (Client-Side Prediction)

### 3.1 예측 원칙
```
1. 플레이어 자신의 명령은 즉시 실행 (로컬)
2. 서버 확인 대기 (네트워크 지연 동안)
3. 서버에서 보정 데이터 수신
4. 서버 데이터와 비교하여 보정

장점:
- 플레이어는 즉각적인 피드백 경험
- 실제 지연은 숨겨짐
- 느낌이 반응적임

단점:
- 클라이언트와 서버 상태 불일치 가능
- 부정행위 방지 필요
```

### 3.2 단순 이동 예측
```csharp
public class ClientPrediction
{
    public void PredictUnitMovement(Unit unit, Vector2 targetPos)
    {
        // 클라이언트 로컬 예측
        unit.PredictedPosition = targetPos;
        unit.PredictedState = UnitState.Moving;
        
        // 서버에 명령 전송 (비동기)
        SendCommandToServer(new MoveCommand 
        { 
            UnitId = unit.Id,
            TargetPosition = targetPos 
        });
    }
    
    public void ApplyServerCorrection(UnitUpdate update)
    {
        // 서버 상태 수신
        float predictionError = Vector2.Distance(
            unit.PredictedPosition, 
            update.Position
        );
        
        // 오차가 크면 즉시 보정
        if (predictionError > CORRECTION_THRESHOLD)
        {
            unit.Position = update.Position;
            unit.Velocity = update.Velocity;
        }
        else
        {
            // 작은 오차는 점진적으로 보정
            unit.Position = Vector2.Lerp(
                unit.PredictedPosition,
                update.Position,
                0.2f
            );
        }
    }
}

private const float CORRECTION_THRESHOLD = 5.0f; // 5 타일
```

### 3.3 공격 예측
```csharp
public class CombatPrediction
{
    public void PredictAttack(Unit attacker, Unit target)
    {
        // 클라이언트에서 공격 애니메이션 즉시 재생
        attacker.PlayAttackAnimation();
        
        // 서버에 공격 명령 전송
        SendCommandToServer(new AttackCommand
        {
            AttackerId = attacker.Id,
            TargetId = target.Id
        });
        
        // 대략적인 데미지 예측
        float predictedDamage = attacker.AttackPower * 0.8f; // 80% 추정치
        target.DisplayDamageNumber(predictedDamage);
    }
    
    public void ApplyServerCombatResult(CombatResult result)
    {
        var attacker = GetUnit(result.AttackerId);
        var target = GetUnit(result.TargetId);
        
        // 실제 데미지와 비교
        float actualDamage = result.DamageDealt;
        
        // 차이가 크면 보정
        target.Health = result.TargetHealth;
        
        if (target.Health <= 0)
        {
            target.Die(result.DeathType);
        }
    }
}
```

---

## 4. 서버-클라이언트 동기화

### 4.1 부분 동기화 (Delta Sync)
```csharp
public class DeltaSynchronization
{
    private GameState previousState;
    private GameState currentState;
    
    public GameStateDelta ComputeDelta(Player viewerPlayer)
    {
        var delta = new GameStateDelta();
        
        // 1. 유닛 변경 감지
        foreach (var unit in currentState.AllUnits)
        {
            // 포그 오브 워 확인
            if (!CanPlayerSeeUnit(viewerPlayer, unit))
                continue;
            
            var prevUnit = previousState.GetUnit(unit.Id);
            
            if (HasSignificantChange(unit, prevUnit))
            {
                delta.UnitUpdates.Add(CreateUnitUpdate(unit));
            }
        }
        
        // 2. 건물 변경 감지
        foreach (var building in currentState.AllBuildings)
        {
            if (!CanPlayerSeeBuilding(viewerPlayer, building))
                continue;
            
            var prevBuilding = previousState.GetBuilding(building.Id);
            
            if (HasSignificantChange(building, prevBuilding))
            {
                delta.BuildingUpdates.Add(CreateBuildingUpdate(building));
            }
        }
        
        // 3. 자원 변경
        var playerResources = currentState.GetPlayer(viewerPlayer.Id).Resources;
        if (HasResourceChanged(playerResources))
        {
            delta.ResourceUpdates.Add(CreateResourceUpdate(viewerPlayer));
        }
        
        return delta;
    }
    
    private bool HasSignificantChange(Unit current, Unit previous)
    {
        if (previous == null) return true;
        
        // 변경 감지 임계값
        return Math.Abs(current.Health - previous.Health) >= 1 ||
               Vector2.Distance(current.Position, previous.Position) >= 1.0f ||
               current.State != previous.State ||
               current.CurrentTargetId != previous.CurrentTargetId;
    }
}
```

### 4.2 전체 동기화 (Full State Sync)
```csharp
public class FullSynchronization
{
    public void SendFullStateSync(Player player)
    {
        var fullState = new FullGameState();
        
        // 1. 모든 가시 유닛
        foreach (var unit in gameState.AllUnits)
        {
            if (CanPlayerSeeUnit(player, unit))
            {
                fullState.Units.Add(CreateUnitSnapshot(unit));
            }
        }
        
        // 2. 모든 가시 건물
        foreach (var building in gameState.AllBuildings)
        {
            if (CanPlayerSeeBuilding(player, building))
            {
                fullState.Buildings.Add(CreateBuildingSnapshot(building));
            }
        }
        
        // 3. 플레이어 자신의 정보만 상세 정보 포함
        foreach (var p in gameState.Players)
        {
            var playerInfo = new PlayerSnapshot
            {
                PlayerId = p.Id,
                Resources = p.Resources.ToSnapshot(),
                IsDefeated = p.IsDefeated,
                IsAlly = IsAlly(player, p)
            };
            
            // 자신이나 팀원의 경우 상세 정보
            if (p == player || IsAlly(player, p))
            {
                playerInfo.IncludeDetailedStats = true;
            }
            
            fullState.Players.Add(playerInfo);
        }
        
        // 4. 포그 오브 워 맵
        fullState.FogOfWarMap = ComputeFogOfWarMap(player);
        
        SendPacketToClient(player, fullState);
    }
    
    public void SendFullStateSyncOnReconnect(Player player)
    {
        // 재연결 시 전체 상태 전송
        SendFullStateSync(player);
        
        // 마지막 몇 개의 게임 이벤트도 전송 (컨텍스트)
        SendRecentGameEvents(player, lastEventsCount: 10);
    }
}
```

---

## 5. 재동기화 (Resynchronization)

### 5.1 동기화 오류 감지
```csharp
public class SyncErrorDetection
{
    private Dictionary<int, GameState> clientStates;
    private GameState authorityState;
    
    public void CheckSyncConsistency()
    {
        foreach (var player in gameState.Players)
        {
            var clientState = clientStates[player.Id];
            
            // 유닛 상태 비교
            foreach (var unit in authorityState.AllUnits)
            {
                if (!CanPlayerSeeUnit(player, unit))
                    continue;
                
                var clientUnit = clientState.GetUnit(unit.Id);
                if (clientUnit == null)
                {
                    LogSyncError($"플레이어 {player.Id}: 유닛 {unit.Id} 누락");
                    continue;
                }
                
                // 위치 오차 확인
                float positionError = Vector2.Distance(
                    unit.Position,
                    clientUnit.Position
                );
                
                if (positionError > MAX_POSITION_ERROR)
                {
                    LogSyncError($"유닛 {unit.Id} 위치 오차: {positionError}");
                }
                
                // 체력 불일치 확인
                if (unit.Health != clientUnit.Health)
                {
                    LogSyncError($"유닛 {unit.Id} 체력 불일치");
                }
            }
        }
    }
    
    private const float MAX_POSITION_ERROR = 10.0f;
}
```

### 5.2 부분 재동기화
```csharp
public class PartialResync
{
    public void ResyncUnit(Player player, Unit unit)
    {
        // 특정 유닛만 재동기화
        var packet = new UnitSyncPacket
        {
            UnitId = unit.Id,
            Position = unit.Position,
            Velocity = unit.Velocity,
            Health = unit.Health,
            State = unit.State,
            TargetUnitId = unit.CurrentTargetId,
            Timestamp = GameTick
        };
        
        SendPacketToClient(player, packet);
    }
    
    public void ResyncBuilding(Player player, Building building)
    {
        var packet = new BuildingSyncPacket
        {
            BuildingId = building.Id,
            Health = building.Health,
            ConstructionProgress = building.ConstructionProgress,
            ProductionQueue = building.ProductionQueue.ToSnapshot(),
            CurrentResearch = building.CurrentResearch,
            Timestamp = GameTick
        };
        
        SendPacketToClient(player, packet);
    }
    
    public void ResyncResources(Player player)
    {
        var packet = new ResourceSyncPacket
        {
            PlayerId = player.Id,
            Minerals = player.Resources.Minerals,
            Gas = player.Resources.Gas,
            Food = player.Resources.Food,
            CurrentPopulation = player.GetCurrentPopulation(),
            MaxPopulation = player.GetMaxPopulation(),
            Timestamp = GameTick
        };
        
        SendPacketToClient(player, packet);
    }
}
```

### 5.3 전체 재동기화
```csharp
public class FullResync
{
    public void ForceSyncAllPlayers()
    {
        foreach (var player in gameState.Players)
        {
            ForceSyncPlayer(player);
        }
    }
    
    public void ForceSyncPlayer(Player player)
    {
        // 경고 로그
        LogWarning($"플레이어 {player.Id}에 대한 강제 동기화 시작");
        
        // 1. 전체 상태 전송
        SendFullStateSync(player);
        
        // 2. 최근 게임 이벤트 전송
        SendRecentGameEvents(player, lastEventsCount: 20);
        
        // 3. 클라이언트 확인 대기
        WaitForClientAcknowledgement(player, timeoutMs: 5000);
        
        LogInfo($"플레이어 {player.Id} 동기화 완료");
    }
}
```

---

## 6. 관전자 (Spectator) 동기화

### 6.1 관전 모드
```csharp
public class SpectatorMode
{
    public class Spectator
    {
        public int SpectatorId { get; set; }
        public int WatchedGameId { get; set; }
        public int? FollowingPlayerId { get; set; }
        public Vector2 CameraPosition { get; set; }
        public float ZoomLevel { get; set; }
        public bool CanSeeFog { get; set; } // true = 안개 무시
    }
    
    public void SendSpectatorUpdate(Spectator spectator)
    {
        var game = GetGame(spectator.WatchedGameId);
        
        // 전체 게임 상태 전송 (포그 오브 워 무시)
        var update = new SpectatorGameState();
        
        // 모든 유닛 표시
        update.AllUnits = game.AllUnits
            .Select(CreateUnitUpdate)
            .ToList();
        
        // 모든 건물 표시
        update.AllBuildings = game.AllBuildings
            .Select(CreateBuildingUpdate)
            .ToList();
        
        // 모든 플레이어 자원 표시
        update.AllPlayersResources = game.Players
            .Select(p => new PlayerResourceSnapshot
            {
                PlayerId = p.Id,
                Minerals = p.Resources.Minerals,
                Gas = p.Resources.Gas,
                Food = p.Resources.Food,
                CurrentPopulation = p.GetCurrentPopulation(),
                MaxPopulation = p.GetMaxPopulation(),
                Score = p.Score
            })
            .ToList();
        
        // 카메라 위치 정보
        update.CameraFocus = spectator.FollowingPlayerId.HasValue
            ? GetPlayer(spectator.FollowingPlayerId.Value).MainBase.Position
            : spectator.CameraPosition;
        
        SendPacketToSpectator(spectator, update);
    }
    
    public void SwitchFollowPlayer(Spectator spectator, int newPlayerId)
    {
        spectator.FollowingPlayerId = newPlayerId;
        // 다음 업데이트부터 새 플레이어 따라감
    }
    
    public void SetSpectatorCamera(Spectator spectator, Vector2 position)
    {
        spectator.FollowingPlayerId = null; // 수동 제어
        spectator.CameraPosition = position;
    }
}
```

### 6.2 관전 기능
```csharp
public class SpectatorFeatures
{
    // 플레이어 수 제한
    public const int MAX_SPECTATORS_PER_GAME = 50;
    
    // 지원 기능
    public bool CanSpectateGame(Spectator spectator, Game game)
    {
        // 게임이 진행 중인지 확인
        if (game.Status != GameStatus.InProgress)
            return false;
        
        // 관전자 수 제한 확인
        if (game.Spectators.Count >= MAX_SPECTATORS_PER_GAME)
            return false;
        
        // 친구 게임이거나 공개 게임인지 확인
        return IsPublicGame(game) || IsFriendGame(spectator, game);
    }
    
    // 관전 UI 업데이트
    public void SendSpectatorUIUpdate(Spectator spectator)
    {
        var game = GetGame(spectator.WatchedGameId);
        
        var ui = new SpectatorUIUpdate
        {
            GameTime = FormatTime(game.ElapsedTime),
            Players = game.Players
                .Select(p => new SpectatorPlayerUI
                {
                    PlayerId = p.Id,
                    Name = p.Name,
                    Color = p.Color,
                    IsDefeated = p.IsDefeated,
                    Resources = p.Resources,
                    Supply = $"{p.GetCurrentPopulation()}/{p.GetMaxPopulation()}",
                    Score = p.Score,
                    UnitCount = p.Units.Count,
                    BuildingCount = p.Buildings.Count
                })
                .ToList(),
            RecentEvents = game.GetRecentEvents(count: 20)
        };
        
        SendPacketToSpectator(spectator, ui);
    }
}
```

---

## 7. 네트워크 불안정성 처리

### 7.1 패킷 손실 처리
```csharp
public class PacketLossHandling
{
    public class SentPacket
    {
        public int SequenceNumber { get; set; }
        public long SentTime { get; set; }
        public int RetryCount { get; set; }
        public GameStateDelta Delta { get; set; }
    }
    
    private Dictionary<int, Queue<SentPacket>> unacknowledgedPackets;
    
    public void SendReliableUpdate(Player player, GameStateDelta delta)
    {
        var packet = new SentPacket
        {
            SequenceNumber = nextSequenceNumber++,
            SentTime = DateTime.UtcNow.Ticks,
            RetryCount = 0,
            Delta = delta
        };
        
        if (!unacknowledgedPackets.ContainsKey(player.Id))
            unacknowledgedPackets[player.Id] = new Queue<SentPacket>();
        
        unacknowledgedPackets[player.Id].Enqueue(packet);
        SendPacketToClient(player, packet);
    }
    
    public void OnPacketAcknowledged(int playerId, int sequenceNumber)
    {
        var packets = unacknowledgedPackets[playerId];
        
        // 확인된 패킷까지 제거
        while (packets.Count > 0 && packets.Peek().SequenceNumber <= sequenceNumber)
        {
            packets.Dequeue();
        }
    }
    
    public void RetryUnacknowledgedPackets()
    {
        var now = DateTime.UtcNow.Ticks;
        const long RETRY_INTERVAL = 500; // 500ms
        
        foreach (var playerId in unacknowledgedPackets.Keys)
        {
            var packets = unacknowledgedPackets[playerId];
            
            foreach (var packet in packets.ToList())
            {
                long timeSinceSent = (now - packet.SentTime) / 10000; // ms
                
                if (timeSinceSent >= RETRY_INTERVAL * (packet.RetryCount + 1))
                {
                    if (packet.RetryCount < MAX_RETRIES)
                    {
                        packet.RetryCount++;
                        SendPacketToClient(GetPlayer(playerId), packet);
                    }
                    else
                    {
                        // 재전송 포기
                        LogWarning($"플레이어 {playerId} 패킷 {packet.SequenceNumber} 재전송 포기");
                    }
                }
            }
        }
    }
    
    private const int MAX_RETRIES = 3;
}
```

### 7.2 높은 지연 처리
```csharp
public class HighLatencyHandling
{
    public void CheckHighLatency(Player player, int latencyMs)
    {
        if (latencyMs > 500) // 500ms 이상
        {
            LogWarning($"플레이어 {player.Id}: 높은 지연 감지 ({latencyMs}ms)");
            
            // 대역폭 절감 모드 활성화
            EnableBandwidthSavingMode(player);
            
            // 업데이트 빈도 감소
            ReduceUpdateFrequency(player);
        }
    }
    
    public void EnableBandwidthSavingMode(Player player)
    {
        // 더 적은 게임 상태 정보 전송
        player.BandwidthMode = BandwidthMode.Saving;
        
        // 동기화 간격 증가
        player.SyncInterval = 200; // 100ms → 200ms
        
        // 포그 업데이트 간격 증가
        player.FogUpdateInterval = 300;
    }
    
    public void ReduceUpdateFrequency(Player player)
    {
        // 세부 정보 업데이트 빈도 감소
        // - ResourceUpdate: 500ms → 1000ms
        // - DetailedUnitInfo: 200ms → 500ms
        // - BuildingDetails: 200ms → 500ms
    }
}
```

### 7.3 연결 끊김 감지 및 재연결
```csharp
public class DisconnectionHandling
{
    public const int PING_TIMEOUT_MS = 5000;
    public const int RECONNECT_TIMEOUT_MS = 30000;
    
    public void CheckPlayerConnectivity()
    {
        foreach (var player in gameState.Players)
        {
            long timeSinceLastPing = DateTime.UtcNow.Ticks - player.LastPingTime;
            
            if (timeSinceLastPing > PING_TIMEOUT_MS)
            {
                if (!player.IsDisconnected)
                {
                    player.IsDisconnected = true;
                    player.DisconnectTime = DateTime.UtcNow;
                    
                    NotifyOtherPlayers($"{player.Name}가 연결 해제되었습니다");
                }
                
                long timeSinceDisconnect = 
                    (DateTime.UtcNow - player.DisconnectTime).TotalMilliseconds;
                
                if (timeSinceDisconnect > RECONNECT_TIMEOUT_MS)
                {
                    // 재연결 포기, 게임에서 제거
                    RemovePlayerFromGame(player);
                    CheckGameEnd();
                }
            }
        }
    }
    
    public void HandlePlayerReconnection(Player player)
    {
        player.IsDisconnected = false;
        player.LastPingTime = DateTime.UtcNow.Ticks;
        
        // 전체 상태 재동기화
        SendFullStateSync(player);
        
        // 재연결 알림
        NotifyOtherPlayers($"{player.Name}가 다시 연결되었습니다");
    }
}
```

---

## 8. 성능 모니터링

### 8.1 동기화 메트릭
```csharp
public class SyncMetrics
{
    public struct PlayerSyncMetrics
    {
        public int PlayerId { get; set; }
        public float AverageLatencyMs { get; set; }
        public float PacketLossRate { get; set; }
        public float LastDeltaSizeBytes { get; set; }
        public long LastSyncTimeTicks { get; set; }
        public int OutstandingPackets { get; set; }
    }
    
    public void LogSyncMetrics()
    {
        foreach (var player in gameState.Players)
        {
            var metrics = new PlayerSyncMetrics
            {
                PlayerId = player.Id,
                AverageLatencyMs = player.AverageLatency,
                PacketLossRate = player.PacketLossRate,
                LastDeltaSizeBytes = player.LastDeltaSize,
                LastSyncTimeTicks = player.LastSyncTime,
                OutstandingPackets = player.UnacknowledgedPackets.Count
            };
            
            LogMetrics(metrics);
        }
    }
}
```

### 8.2 성능 최적화 조언
```
동기화 성능 개선:

1. 대역폭 최적화:
   - 델타 동기화 사용
   - 패킷 압축
   - LOD 기반 업데이트 빈도 조정

2. CPU 최적화:
   - 변경 감지 알고리즘 효율화
   - 공간 분할 (Quadtree)
   - 관심 영역 기반 필터링

3. 메모리 최적화:
   - 객체 풀링
   - 제한된 히스토리 유지
   - 불필요한 상태 정보 제거

4. 네트워크 최적화:
   - 명령 배치
   - 재전송 정책 조정
   - 타임아웃 값 적절히 설정
```

---

## 9. 테스트 시나리오

### 9.1 동기화 테스트
```
[테스트 1] 기본 명령 동기화
- 플레이어 1이 유닛 이동 명령
- 모든 클라이언트에서 유닛이 같은 위치로 이동하는지 확인

[테스트 2] 높은 지연 환경
- RTT 200ms+ 환경에서 게임 진행
- 명령이 정확히 실행되고 보정이 발생하는지 확인

[테스트 3] 패킷 손실
- 20% 패킷 손실 상황에서 게임 진행
- 게임이 정상적으로 진행되고 재동기화가 발생하는지 확인

[테스트 4] 재연결
- 게임 중 연결 끊김 → 재연결
- 게임 상태가 정확히 복구되는지 확인

[테스트 5] 관전 모드
- 관전자가 게임을 보며 UI 정보가 정확한지 확인
- 카메라 전환이 부드럽게 작동하는지 확인
```

### 9.2 부하 테스트
```
[부하 테스트 1] 대규모 멀티플레이
- 8명 플레이어, 200+ 유닛 동시 관리
- 대역폭 사용량 및 CPU 사용률 측정

[부하 테스트 2] 포그 오브 워
- 256x256 맵에서 포그 계산 성능 측정
- 여러 플레이어의 포그 업데이트 동시 처리

[부하 테스트 3] 관전자 대량 접속
- 50명+ 관전자가 동시 접속할 때 성능
- 서버 자원 사용량 모니터링
```

