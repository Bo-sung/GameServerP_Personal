# FPS 실시간 동기화 전략

## 개요

FPS 게임은 빠른 반응 속도가 중요하므로, 효율적인 네트워크 동기화 전략이 필수입니다.

---

## 기본 개념

### 클라이언트-서버 모델
```
┌─────────┐        Input         ┌─────────┐
│ Client  │ ──────────────────> │ Server  │
│         │                      │         │
│  (예측)  │ <──────────────────  │ (권한)  │
└─────────┘       State          └─────────┘
```

- **클라이언트**: 로컬 예측 + 서버 조정
- **서버**: 절대적 권한, 모든 계산 수행

---

## 동기화 방식

### 1. 클라이언트 예측 (Client-Side Prediction)

플레이어는 자신의 입력을 즉시 로컬에서 시뮬레이션합니다.

#### 구현
```csharp
public class ClientPlayer
{
    private Queue<PlayerInput> inputHistory = new();
    private uint currentSequence = 0;

    public void Update(float deltaTime)
    {
        // 1. 입력 수집
        var input = GatherInput();
        input.Sequence = currentSequence++;
        input.Timestamp = GetLocalTime();

        // 2. 로컬 예측 (즉시 적용)
        ApplyInput(input);

        // 3. 입력 히스토리 저장
        inputHistory.Enqueue(input);

        // 4. 서버에 전송
        SendInputToServer(input);

        // 5. 오래된 입력 제거 (1초 이상)
        while (inputHistory.Count > 0 &&
               GetLocalTime() - inputHistory.Peek().Timestamp > 1000)
        {
            inputHistory.Dequeue();
        }
    }

    private PlayerInput GatherInput()
    {
        return new PlayerInput
        {
            Movement = new Vector3(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Jump"),
                Input.GetAxis("Vertical")
            ),
            Rotation = new Vector2(
                Camera.Pitch,
                Camera.Yaw
            ),
            Buttons = GetButtonFlags()
        };
    }

    private void ApplyInput(PlayerInput input)
    {
        // 물리 시뮬레이션
        if (input.Movement.magnitude > 0)
        {
            var movement = transform.TransformDirection(input.Movement);
            controller.Move(movement * moveSpeed * Time.deltaTime);
        }

        if ((input.Buttons & InputButtons.Jump) != 0 && isGrounded)
        {
            velocity.y = jumpForce;
        }

        // 중력
        velocity.y -= gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
```

---

### 2. 서버 조정 (Server Reconciliation)

서버로부터 받은 상태가 클라이언트 예측과 다르면 수정합니다.

#### 구현
```csharp
public class ClientPlayer
{
    public void OnServerStateReceived(ServerPlayerState serverState)
    {
        // 1. 서버 상태 적용
        var lastProcessedSeq = serverState.LastProcessedInput;

        // 2. 위치 차이 확인
        var positionError = Vector3.Distance(transform.position, serverState.Position);

        if (positionError > 0.1f)  // 10cm 이상 차이나면
        {
            // 3. 서버 위치로 보정
            transform.position = serverState.Position;
            velocity = serverState.Velocity;

            // 4. 히스토리에서 재처리할 입력 찾기
            var inputsToReplay = new Queue<PlayerInput>();

            foreach (var input in inputHistory)
            {
                if (input.Sequence > lastProcessedSeq)
                {
                    inputsToReplay.Enqueue(input);
                }
            }

            // 5. 서버 이후의 입력 재적용 (리플레이)
            foreach (var input in inputsToReplay)
            {
                ApplyInput(input);
            }

            // 디버그 로그
            Debug.Log($"Server reconciliation: {positionError:F3}m error, replayed {inputsToReplay.Count} inputs");
        }

        // 6. 처리된 입력 제거
        while (inputHistory.Count > 0 &&
               inputHistory.Peek().Sequence <= lastProcessedSeq)
        {
            inputHistory.Dequeue();
        }
    }
}
```

---

### 3. 엔티티 보간 (Entity Interpolation)

다른 플레이어의 움직임을 부드럽게 표시합니다.

#### 구현
```csharp
public class RemotePlayer
{
    private struct StateSnapshot
    {
        public uint Timestamp;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Velocity;
    }

    private Queue<StateSnapshot> stateBuffer = new();
    private const int INTERPOLATION_DELAY = 100; // ms

    public void OnServerStateReceived(ServerPlayerState state)
    {
        // 버퍼에 추가
        stateBuffer.Enqueue(new StateSnapshot
        {
            Timestamp = state.Timestamp,
            Position = state.Position,
            Rotation = state.Rotation,
            Velocity = state.Velocity
        });

        // 버퍼 크기 제한 (최대 1초)
        while (stateBuffer.Count > 60)
        {
            stateBuffer.Dequeue();
        }
    }

    public void Update()
    {
        if (stateBuffer.Count < 2)
            return;

        // 현재 렌더링 시간 (실제 시간 - 100ms)
        uint renderTime = GetCurrentTime() - INTERPOLATION_DELAY;

        // 보간할 두 스냅샷 찾기
        StateSnapshot? from = null;
        StateSnapshot? to = null;

        foreach (var snapshot in stateBuffer)
        {
            if (snapshot.Timestamp <= renderTime)
            {
                from = snapshot;
            }
            else
            {
                to = snapshot;
                break;
            }
        }

        if (from == null || to == null)
            return;

        // 선형 보간
        float t = (renderTime - from.Value.Timestamp) /
                  (float)(to.Value.Timestamp - from.Value.Timestamp);

        transform.position = Vector3.Lerp(from.Value.Position, to.Value.Position, t);
        transform.eulerAngles = Vector3.Lerp(from.Value.Rotation, to.Value.Rotation, t);

        // 오래된 스냅샷 제거
        while (stateBuffer.Count > 0 &&
               stateBuffer.Peek().Timestamp < renderTime - 200)
        {
            stateBuffer.Dequeue();
        }
    }
}
```

---

### 4. 외삽 (Extrapolation)

새로운 상태가 도착하지 않으면 예측으로 움직임을 연장합니다.

#### 구현
```csharp
public class RemotePlayer
{
    private StateSnapshot lastSnapshot;
    private uint lastReceiveTime;

    public void Update()
    {
        uint currentTime = GetCurrentTime();
        uint timeSinceLastUpdate = currentTime - lastReceiveTime;

        // 200ms 이상 업데이트 없으면 외삽
        if (timeSinceLastUpdate > 200)
        {
            // 속도 기반 위치 예측
            float deltaTime = (timeSinceLastUpdate - 200) / 1000f;
            transform.position = lastSnapshot.Position + lastSnapshot.Velocity * deltaTime;

            // 외삽 표시 (디버그)
            Debug.DrawLine(lastSnapshot.Position, transform.position, Color.yellow);
        }
    }
}
```

---

## 서버 구현

### 입력 처리 및 상태 브로드캐스트

```csharp
public class GameServer
{
    private const int TICK_RATE = 64;  // 64 Hz
    private const float TICK_INTERVAL = 1000f / TICK_RATE;  // 15.625ms

    private Dictionary<string, ServerPlayer> players = new();
    private float accumulator = 0;

    public void Update(float deltaTime)
    {
        accumulator += deltaTime * 1000;  // ms로 변환

        while (accumulator >= TICK_INTERVAL)
        {
            Tick();
            accumulator -= TICK_INTERVAL;
        }
    }

    private void Tick()
    {
        uint serverTime = GetServerTime();

        // 1. 모든 플레이어 입력 처리
        foreach (var player in players.Values)
        {
            ProcessPlayerInputs(player);
        }

        // 2. 물리 시뮬레이션
        PhysicsUpdate(TICK_INTERVAL / 1000f);

        // 3. 상태 브로드캐스트 (모든 플레이어에게)
        BroadcastPlayerStates(serverTime);
    }

    private void ProcessPlayerInputs(ServerPlayer player)
    {
        // 큐에서 처리되지 않은 입력 가져오기
        while (player.InputQueue.TryDequeue(out var input))
        {
            // 입력 검증
            if (!ValidateInput(player, input))
            {
                continue;
            }

            // 입력 적용
            ApplyInput(player, input);

            // 마지막 처리된 시퀀스 기록
            player.LastProcessedInput = input.Sequence;
        }
    }

    private void BroadcastPlayerStates(uint timestamp)
    {
        // 스냅샷 압축 (여러 플레이어를 하나의 패킷으로)
        var snapshot = new PlayerSnapshot
        {
            Timestamp = timestamp,
            PlayerCount = (byte)players.Count,
            Players = new List<PlayerState>()
        };

        foreach (var player in players.Values)
        {
            snapshot.Players.Add(new PlayerState
            {
                PlayerId = player.Id,
                Position = player.Position,
                Rotation = player.Rotation,
                Velocity = player.Velocity,
                Health = player.Health,
                StateFlags = GetStateFlags(player),
                LastProcessedInput = player.LastProcessedInput
            });
        }

        // 모든 클라이언트에 UDP로 전송
        BroadcastUDP(PacketType.PlayerSnapshot, snapshot);
    }

    private byte GetStateFlags(ServerPlayer player)
    {
        byte flags = 0;
        if (player.IsCrouching) flags |= 0x01;
        if (player.IsSprinting) flags |= 0x02;
        if (player.IsAiming) flags |= 0x04;
        if (player.IsReloading) flags |= 0x08;
        if (player.IsAlive) flags |= 0x10;
        return flags;
    }
}
```

---

## 최적화 전략

### 1. 관심 영역 관리 (Area of Interest)

```csharp
public class InterestManager
{
    private const float CLOSE_RANGE = 20f;    // 20m
    private const float MEDIUM_RANGE = 50f;   // 50m
    private const float FAR_RANGE = 100f;     // 100m

    public int GetUpdateRate(Vector3 observerPos, Vector3 targetPos, bool isInView)
    {
        float distance = Vector3.Distance(observerPos, targetPos);

        if (!isInView)
        {
            return 2;  // 시야 밖: 2 Hz
        }
        else if (distance < CLOSE_RANGE)
        {
            return 64;  // 근거리: 64 Hz
        }
        else if (distance < MEDIUM_RANGE)
        {
            return 32;  // 중거리: 32 Hz
        }
        else if (distance < FAR_RANGE)
        {
            return 16;  // 원거리: 16 Hz
        }
        else
        {
            return 4;   // 매우 먼 거리: 4 Hz
        }
    }

    public List<ServerPlayer> GetRelevantPlayers(ServerPlayer observer)
    {
        var relevant = new List<ServerPlayer>();

        foreach (var player in GameServer.AllPlayers)
        {
            if (player == observer)
                continue;

            float distance = Vector3.Distance(observer.Position, player.Position);

            // 150m 이내만 동기화
            if (distance <= 150f)
            {
                relevant.Add(player);
            }
        }

        return relevant;
    }
}
```

### 2. 델타 압축

```csharp
public class DeltaCompression
{
    private Dictionary<string, PlayerState> lastSentStates = new();

    public byte[] CompressSnapshot(PlayerSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(snapshot.Timestamp);
        writer.Write(snapshot.PlayerCount);

        foreach (var player in snapshot.Players)
        {
            writer.Write(player.PlayerId);

            // 이전 상태와 비교
            if (lastSentStates.TryGetValue(player.PlayerId, out var lastState))
            {
                // 델타 압축
                var deltaPos = player.Position - lastState.Position;
                WriteVector3Compressed(writer, deltaPos);

                var deltaRot = player.Rotation - lastState.Rotation;
                WriteVector3Compressed(writer, deltaRot);
            }
            else
            {
                // 전체 값
                WriteVector3Full(writer, player.Position);
                WriteVector3Full(writer, player.Rotation);
            }

            writer.Write(player.Health);
            writer.Write(player.StateFlags);

            // 상태 저장
            lastSentStates[player.PlayerId] = player;
        }

        return stream.ToArray();
    }

    private void WriteVector3Compressed(BinaryWriter writer, Vector3 vec)
    {
        // 16비트 고정소수점 (±327.67 범위, 0.01 정밀도)
        writer.Write((short)(vec.x * 100));
        writer.Write((short)(vec.y * 100));
        writer.Write((short)(vec.z * 100));
    }

    private void WriteVector3Full(BinaryWriter writer, Vector3 vec)
    {
        writer.Write(vec.x);
        writer.Write(vec.y);
        writer.Write(vec.z);
    }
}
```

### 3. 우선순위 큐

```csharp
public class PriorityQueue
{
    private class UpdateTask
    {
        public ServerPlayer Observer { get; set; }
        public ServerPlayer Target { get; set; }
        public int Priority { get; set; }
        public uint LastUpdateTime { get; set; }
    }

    private PriorityQueue<UpdateTask> queue = new();

    public void Update()
    {
        uint currentTime = GetServerTime();

        // 우선순위 재계산
        foreach (var observer in GameServer.AllPlayers)
        {
            foreach (var target in GetRelevantPlayers(observer))
            {
                float distance = Vector3.Distance(observer.Position, target.Position);
                bool isInView = IsInViewFrustum(observer, target);

                int priority = CalculatePriority(distance, isInView);
                int updateRate = GetUpdateRate(distance, isInView);
                uint updateInterval = 1000u / (uint)updateRate;

                var task = new UpdateTask
                {
                    Observer = observer,
                    Target = target,
                    Priority = priority
                };

                // 업데이트 간격이 지났으면 큐에 추가
                if (currentTime - task.LastUpdateTime >= updateInterval)
                {
                    queue.Enqueue(task, priority);
                }
            }
        }

        // 우선순위 높은 순서대로 처리
        while (queue.Count > 0)
        {
            var task = queue.Dequeue();
            SendPlayerStateToObserver(task.Observer, task.Target);
            task.LastUpdateTime = currentTime;
        }
    }

    private int CalculatePriority(float distance, bool isInView)
    {
        int priority = 100;

        // 거리에 따른 우선순위
        priority -= (int)distance;

        // 시야 내면 우선순위 상승
        if (isInView)
        {
            priority += 50;
        }

        return priority;
    }
}
```

---

## 네트워크 품질 대응

### 패킷 손실 대응
```csharp
public class PacketLossCompensation
{
    private const int MAX_PACKET_LOSS = 10;  // 10%

    public void OnPacketLoss(int lossPercentage)
    {
        if (lossPercentage > MAX_PACKET_LOSS)
        {
            // 중요 패킷 재전송
            ResendCriticalPackets();

            // 업데이트 빈도 증가
            IncreaseUpdateRate();
        }
    }

    private void ResendCriticalPackets()
    {
        // PlayerDeath, GameStateChange 등 중요 패킷 재전송
    }
}
```

### 높은 지연 대응
```csharp
public class HighLatencyCompensation
{
    private const int HIGH_LATENCY_THRESHOLD = 150;  // 150ms

    public void OnHighLatency(int latency)
    {
        if (latency > HIGH_LATENCY_THRESHOLD)
        {
            // 외삽 시간 증가
            extrapolationTime = latency * 1.5f;

            // 렉 보상 윈도우 확대
            lagCompensationWindow = latency * 2;

            // 클라이언트에 경고
            SendLatencyWarning();
        }
    }
}
```

---

## 디버그 시각화

### 클라이언트 디버그 표시
```csharp
public class NetworkDebug
{
    public void OnGUI()
    {
        GUILayout.Label($"Ping: {ping}ms");
        GUILayout.Label($"Packet Loss: {packetLoss}%");
        GUILayout.Label($"Prediction Error: {predictionError:F3}m");
        GUILayout.Label($"Input Buffer: {inputHistory.Count}");
        GUILayout.Label($"State Buffer: {stateBuffer.Count}");
    }

    public void OnDrawGizmos()
    {
        // 예측 위치 (파란색)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(predictedPosition, 0.5f);

        // 서버 위치 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(serverPosition, 0.5f);

        // 보간 경로 (녹색)
        Gizmos.color = Color.green;
        for (int i = 0; i < stateBuffer.Count - 1; i++)
        {
            Gizmos.DrawLine(stateBuffer[i].Position, stateBuffer[i + 1].Position);
        }
    }
}
```

---

## 성능 측정

### 네트워크 통계
```csharp
public class NetworkStats
{
    private int packetsSent = 0;
    private int packetsReceived = 0;
    private int bytesS sent = 0;
    private int bytesReceived = 0;

    public void LogStats()
    {
        Console.WriteLine($"Packets Sent: {packetsSent}");
        Console.WriteLine($"Packets Received: {packetsReceived}");
        Console.WriteLine($"Bandwidth Out: {bytesSent / 1024f:F2} KB/s");
        Console.WriteLine($"Bandwidth In: {bytesReceived / 1024f:F2} KB/s");
        Console.WriteLine($"Average Ping: {averagePing}ms");
    }
}
```
