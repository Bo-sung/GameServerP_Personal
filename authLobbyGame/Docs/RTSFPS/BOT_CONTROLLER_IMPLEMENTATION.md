# C# Bot Controller 구현 가이드

## 1. 프로젝트 설정

### 1.1 프로젝트 구조

```
BotControllerServer/
├── src/
│   ├── BotController.cs          # 메인 서버
│   ├── BotAgent.cs               # 개별 봇 에이전트
│   ├── FSM/
│   │   ├── BotFSM.cs
│   │   ├── States/
│   │   │   ├── PatrolState.cs
│   │   │   ├── InvestigateState.cs
│   │   │   ├── CombatState.cs
│   │   │   ├── RetreatState.cs
│   │   │   └── HealingState.cs
│   │   └── BotState.cs
│   ├── AI/
│   │   ├── AccuracySystem.cs
│   │   ├── SuppressionSystem.cs
│   │   ├── Pathfinding.cs
│   │   └── BotProfile.cs
│   ├── Network/
│   │   ├── NetworkManager.cs
│   │   ├── Packets/
│   │   │   ├── BotStatePacket.cs
│   │   │   ├── BotInputPacket.cs
│   │   │   ├── RTSCommandPacket.cs
│   │   │   └── EventPacket.cs
│   │   └── ConnectionHandler.cs
│   └── Utilities/
│       ├── VectorMath.cs
│       ├── Logger.cs
│       └── PerformanceMonitor.cs
├── tests/
│   ├── FSMTests.cs
│   ├── AccuracyTests.cs
│   └── NetworkTests.cs
├── config/
│   ├── bot_profiles.json
│   ├── difficulty_settings.json
│   └── server_config.json
└── BotControllerServer.csproj
```

### 1.2 .csproj 파일

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- 네트워킹 -->
    <PackageReference Include="LiteNetLib" Version="1.4.2" />
    
    <!-- 로깅 -->
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    
    <!-- JSON 직렬화 -->
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    
    <!-- 성능 분석 -->
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
    
    <!-- 테스트 -->
    <PackageReference Include="xunit" Version="2.6.4" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
  </ItemGroup>

</Project>
```

---

## 2. 핵심 클래스 구현

### 2.1 BotController 메인 클래스

```csharp
// src/BotController.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using Serilog;

public class BotControllerServer {
    private const int BotUpdateTickRate = 30;  // 30Hz
    private const float TickDelta = 1f / BotUpdateTickRate;
    
    private readonly Dictionary<int, BotAgent> activeBots = new();
    private readonly NetManager netManager;
    private readonly ILogger logger;
    
    private Stopwatch gameTimer = new();
    private CancellationTokenSource cancellationToken = new();
    
    public BotControllerServer(int port = 7779) {
        var listener = new EventBasedNetListener();
        netManager = new NetManager(listener);
        
        logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/bot_controller.log")
            .MinimumLevel.Information()
            .CreateLogger();
        
        SetupNetworkListeners(listener);
    }
    
    public async Task Start() {
        if (!netManager.Start(7779)) {
            logger.Error("Failed to start network manager on port 7779");
            return;
        }
        
        logger.Information("Bot Controller Server started on port 7779");
        gameTimer.Start();
        
        await RunGameLoop(cancellationToken.Token);
    }
    
    private async Task RunGameLoop(CancellationToken ct) {
        long targetTickMs = (long)(TickDelta * 1000);
        
        while (!ct.IsCancellationRequested) {
            long tickStartMs = gameTimer.ElapsedMilliseconds;
            
            // 봇 업데이트
            UpdateAllBots(TickDelta);
            
            // 네트워크 업데이트
            netManager.PollEvents();
            
            // Unreal로 상태 전송
            SendBotStatesToUnreal();
            
            // 프레임 타이밍
            long tickElapsedMs = gameTimer.ElapsedMilliseconds - tickStartMs;
            long sleepMs = targetTickMs - tickElapsedMs;
            
            if (sleepMs > 0) {
                await Task.Delay((int)sleepMs, ct);
            } else {
                logger.Warning($"Slow frame: {tickElapsedMs}ms (target: {targetTickMs}ms)");
            }
        }
    }
    
    private void UpdateAllBots(float deltaTime) {
        foreach (var bot in activeBots.Values) {
            try {
                bot.UpdateFSM(deltaTime);
                bot.UpdateMovement(deltaTime);
                bot.UpdateAccuracy();
            } catch (Exception ex) {
                logger.Error(ex, $"Error updating bot {bot.Id}");
            }
        }
    }
    
    private void SendBotStatesToUnreal() {
        var batch = new List<byte>();
        
        foreach (var bot in activeBots.Values) {
            var packet = SerializeBotState(bot);
            batch.AddRange(packet);
        }
        
        // TODO: Unreal 서버로 전송
    }
    
    public void SpawnBot(int botId, Vector3 position, BotProfile profile) {
        var bot = new BotAgent {
            Id = botId,
            Position = position,
            Profile = profile
        };
        
        activeBots[botId] = bot;
        logger.Information($"Bot {botId} spawned at {position}");
    }
    
    public void Stop() {
        cancellationToken.Cancel();
        netManager.Stop();
        logger.Information("Bot Controller Server stopped");
    }
    
    private void SetupNetworkListeners(EventBasedNetListener listener) {
        listener.ConnectionRequestEvent += (request) => {
            request.AcceptIfGtmFree();
        };
        
        listener.PeerConnectedEvent += (peer) => {
            logger.Information($"Client connected: {peer.EndPoint}");
        };
        
        listener.NetworkReceiveEvent += (peer, reader, channel) => {
            OnNetworkReceive(reader.GetRemainingBytes());
            reader.Recycle();
        };
    }
    
    private void OnNetworkReceive(byte[] data) {
        if (data.Length < 1) return;
        
        byte packetType = data[0];
        
        if (packetType == 0x5102) {  // BotInput
            HandleBotInput(data);
        } else if (packetType == 0x5201) {  // RTSCommand
            HandleRTSCommand(data);
        }
    }
    
    private void HandleBotInput(byte[] data) {
        // TODO: 패킷 역직렬화 및 처리
    }
    
    private void HandleRTSCommand(byte[] data) {
        // TODO: 패킷 역직렬화 및 처리
    }
}
```

### 2.2 BotAgent 클래스

```csharp
// src/BotAgent.cs
using System;
using System.Collections.Generic;

public class BotAgent {
    public int Id { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 TargetPosition { get; set; }
    public float Health { get; set; } = 100f;
    public float Suppression { get; set; }
    public bool InCover { get; set; }
    public BotState State { get; set; } = BotState.Patrol;
    
    public BotProfile Profile { get; set; }
    public BotFSM FSM { get; private set; }
    public AccuracySystem Accuracy { get; private set; }
    public SuppressionSystem Suppression_System { get; private set; }
    
    private List<Vector3> movementPath = new();
    private int currentPathIndex = 0;
    
    public BotAgent() {
        FSM = new BotFSM(this);
        Accuracy = new AccuracySystem();
        Suppression_System = new SuppressionSystem();
    }
    
    public void UpdateFSM(float deltaTime) {
        State = FSM.Update(deltaTime, this);
    }
    
    public void UpdateMovement(float deltaTime) {
        if (movementPath.Count == 0) return;
        
        // 다음 경로 지점으로 이동
        Vector3 nextWaypoint = movementPath[currentPathIndex];
        Vector3 direction = (nextWaypoint - Position).normalized;
        
        float moveSpeed = 5f;  // m/s
        
        // 엄폐 페널티
        if (InCover) {
            moveSpeed *= 0.5f;
        }
        
        // 억압 페널티
        float suppressionPenalty = Suppression_System.GetMovementSpeedPenalty();
        moveSpeed *= (1f - suppressionPenalty);
        
        // 위치 업데이트
        Position += direction * moveSpeed * deltaTime;
        
        // 경로 지점 도달 확인
        if (Vector3.Distance(Position, nextWaypoint) < 0.5f) {
            currentPathIndex++;
            if (currentPathIndex >= movementPath.Count) {
                movementPath.Clear();
                currentPathIndex = 0;
            }
        }
    }
    
    public void UpdateAccuracy() {
        Accuracy.Update(this, Suppression_System);
    }
    
    public void SetMovePath(List<Vector3> path) {
        movementPath = path;
        currentPathIndex = 0;
    }
    
    public void ApplyDamage(float damage) {
        Health -= damage;
        Suppression += damage * 0.3f;
        
        if (Health <= 0) {
            State = BotState.Dead;
        }
    }
}
```

### 2.3 FSM 구현

```csharp
// src/FSM/BotFSM.cs
using System;

public class BotFSM {
    private BotAgent bot;
    private IBotState currentState;
    
    public BotFSM(BotAgent agent) {
        bot = agent;
        currentState = new PatrolState();
    }
    
    public BotState Update(float deltaTime, BotAgent bot) {
        // 상태 전환 검사
        IBotState nextState = currentState.CheckTransitions(bot);
        if (nextState != null && nextState.GetType() != currentState.GetType()) {
            currentState.OnExit(bot);
            currentState = nextState;
            currentState.OnEnter(bot);
        }
        
        // 현재 상태 업데이트
        currentState.Update(deltaTime, bot);
        
        return bot.State;
    }
}

public interface IBotState {
    void OnEnter(BotAgent bot);
    void Update(float deltaTime, BotAgent bot);
    void OnExit(BotAgent bot);
    IBotState CheckTransitions(BotAgent bot);
}

// src/FSM/States/PatrolState.cs
public class PatrolState : IBotState {
    private Vector3 patrolTarget;
    private float stateTime = 0f;
    
    public void OnEnter(BotAgent bot) {
        bot.State = BotState.Patrol;
        SelectNewPatrolPoint(bot);
    }
    
    public void Update(float deltaTime, BotAgent bot) {
        stateTime += deltaTime;
        
        // 순찰 지점으로 이동
        Vector3 direction = (patrolTarget - bot.Position).normalized;
        bot.Velocity = direction * 3f;  // 순찰 속도 = 3 m/s
    }
    
    public void OnExit(BotAgent bot) {
        bot.Velocity = Vector3.Zero;
    }
    
    public IBotState CheckTransitions(BotAgent bot) {
        // TODO: 위협 감지 시 Investigate 상태로 전환
        // TODO: 목표 감지 시 Combat 상태로 전환
        return null;
    }
    
    private void SelectNewPatrolPoint(BotAgent bot) {
        // TODO: A* 경로 계산으로 새로운 순찰 지점 선택
    }
}

// src/FSM/States/CombatState.cs
public class CombatState : IBotState {
    private GameObject target;
    private float shootCooldown = 0f;
    private float stateTime = 0f;
    
    public void OnEnter(BotAgent bot) {
        bot.State = BotState.Combat;
    }
    
    public void Update(float deltaTime, BotAgent bot) {
        stateTime += deltaTime;
        shootCooldown -= deltaTime;
        
        if (target == null) return;
        
        // 목표를 향해 이동
        Vector3 toTarget = target.Position - bot.Position;
        float distance = toTarget.magnitude;
        
        // 적절한 거리 유지
        if (distance > 10f) {
            bot.Velocity = toTarget.normalized * 5f;  // 접근
        } else if (distance < 5f) {
            bot.Velocity = -toTarget.normalized * 3f;  // 후퇴
        } else {
            bot.Velocity = Vector3.Zero;  // 정적 위치 유지
        }
        
        // 발사
        if (shootCooldown <= 0f) {
            FireAtTarget(bot, target);
            shootCooldown = 0.1f;  // 100ms 쿨다운
        }
    }
    
    public void OnExit(BotAgent bot) {
        target = null;
    }
    
    public IBotState CheckTransitions(BotAgent bot) {
        // 체력 낮음 → 후퇴
        if (bot.Health < 30f) {
            return new RetreatState { lastTarget = target };
        }
        
        // 목표 상실 → 순찰
        if (target == null) {
            return new PatrolState();
        }
        
        return null;
    }
    
    private void FireAtTarget(BotAgent bot, GameObject target) {
        // 정확도 계산
        float accuracy = bot.Accuracy.Calculate(bot, target.Position);
        
        // 부정확성 추가
        Vector3 fireDir = (target.Position - bot.Position).normalized;
        float inaccuracy = (1f - accuracy) * 0.5f;  // 최대 ±0.5 라디안
        fireDir = AddSpread(fireDir, inaccuracy);
        
        // 히트스캔 실행
        RaycastHit hit;
        if (Physics.Raycast(bot.Position + Vector3.up * 1.7f, fireDir, out hit)) {
            // 명중!
            ApplyDamage(hit.collider.gameObject, 25f);
        }
    }
}
```

---

## 3. AI 시스템 구현

### 3.1 정확도 시스템

```csharp
// src/AI/AccuracySystem.cs
using System;

public class AccuracySystem {
    private float observationTime = 0f;
    
    public void Update(BotAgent bot, SuppressionSystem suppression) {
        if (bot.State == BotState.Combat) {
            observationTime += Time.deltaTime;
        } else {
            observationTime = 0f;
        }
    }
    
    public float Calculate(BotAgent bot, Vector3 targetPosition) {
        // 관찰 시간 기반 정확도 (0~100% 범위)
        float observationAccuracy = Mathf.Sqrt(observationTime / 10f);
        observationAccuracy = Mathf.Clamp01(observationAccuracy);
        
        // 거리 페널티
        float distance = Vector3.Distance(bot.Position, targetPosition);
        float distanceAccuracy = 1f - (distance / 100f);
        distanceAccuracy = Mathf.Clamp01(distanceAccuracy);
        
        // 억압 페널티
        float suppressionAccuracy = 1f - (bot.Suppression / 100f);
        
        // 봇 프로필 보정
        float profileBonus = 1f + (bot.Profile.AccuracyBonus / 100f);
        
        // 최종 정확도
        float finalAccuracy = observationAccuracy * distanceAccuracy * suppressionAccuracy * profileBonus;
        return Mathf.Clamp01(finalAccuracy);
    }
}
```

### 3.2 억압 시스템

```csharp
// src/AI/SuppressionSystem.cs
public class SuppressionSystem {
    private const float LightThreshold = 30f;
    private const float ModerateThreshold = 60f;
    private const float HeavyThreshold = 90f;
    
    public void Update(float deltaTime) {
        // 억압 감소
        float decayRate = 5f;  // 초당 5% 감소
        // TODO: 엄폐 상태면 decayRate *= 2
        
        suppression -= decayRate * deltaTime;
        suppression = Mathf.Max(0f, suppression);
    }
    
    public void IncreaseFromFire(float damage) {
        suppression += damage * 0.3f;
        suppression = Mathf.Min(suppression, 100f);
    }
    
    public float GetMovementSpeedPenalty() {
        if (suppression < LightThreshold) return 0f;
        if (suppression < ModerateThreshold) return 0.3f;  // -30%
        if (suppression < HeavyThreshold) return 0.5f;     // -50%
        return 0.8f;  // -80%
    }
    
    public float GetAccuracyPenalty() {
        if (suppression < LightThreshold) return 0f;
        if (suppression < ModerateThreshold) return 0.2f;  // -20%
        if (suppression < HeavyThreshold) return 0.3f;     // -30%
        return 0.5f;  // -50%
    }
}
```

---

## 4. 네트워크 구현

### 4.1 패킷 직렬화

```csharp
// src/Network/Packets/BotStatePacket.cs
using System;

public class BotStatePacket {
    public const byte PacketType = 0x5101;
    
    public int BotId { get; set; }
    public Vector3 Position { get; set; }
    public byte Health { get; set; }
    public byte Suppression { get; set; }
    public byte State { get; set; }
    public bool InCover { get; set; }
    
    public byte[] Serialize() {
        byte[] buffer = new byte[25];
        buffer[0] = PacketType;
        
        // Bot ID
        BitConverter.GetBytes(BotId).CopyTo(buffer, 1);
        
        // Position (compressed to 6 bytes)
        short x = (short)(Position.X * 10);
        short y = (short)(Position.Y * 10);
        short z = (short)(Position.Z * 10);
        BitConverter.GetBytes(x).CopyTo(buffer, 5);
        BitConverter.GetBytes(y).CopyTo(buffer, 7);
        BitConverter.GetBytes(z).CopyTo(buffer, 9);
        
        // Health & Suppression
        buffer[11] = Health;
        buffer[12] = Suppression;
        
        // State & InCover
        buffer[13] = State;
        buffer[14] = (byte)(InCover ? 1 : 0);
        
        return buffer;
    }
    
    public static BotStatePacket Deserialize(byte[] data) {
        return new BotStatePacket {
            BotId = BitConverter.ToInt32(data, 1),
            Position = new Vector3(
                BitConverter.ToInt16(data, 5) / 10f,
                BitConverter.ToInt16(data, 7) / 10f,
                BitConverter.ToInt16(data, 9) / 10f
            ),
            Health = data[11],
            Suppression = data[12],
            State = data[13],
            InCover = data[14] == 1
        };
    }
}
```

---

## 5. 배포 및 실행

### 5.1 빌드 명령어

```bash
# 프로젝트 빌드
dotnet build BotControllerServer.csproj --configuration Release

# 실행
dotnet run --project BotControllerServer.csproj --configuration Release

# 테스트 실행
dotnet test BotControllerServer.Tests.csproj --configuration Release
```

### 5.2 Docker 배포

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

# 빌드 결과 복사
COPY bin/Release/net8.0/* ./

# 포트 노출
EXPOSE 7779

# 서버 실행
CMD ["dotnet", "BotControllerServer.dll"]
```

```bash
# Docker 이미지 빌드
docker build -t bot-controller:latest .

# 컨테이너 실행
docker run -p 7779:7779 --name bot-controller bot-controller:latest
```

### 5.3 시스템 요구사항

```
최소 요구사항:
• CPU: 2 코어 @ 2.0 GHz
• RAM: 2GB (100 봇 기준)
• 네트워크: 100 Mbps 이상
• OS: Windows 10/11 또는 Linux

권장 사양:
• CPU: 4 코어 @ 3.0 GHz
• RAM: 4GB
• 네트워크: 1 Gbps
• OS: Windows Server 2022 또는 Ubuntu 22.04
```

---

## 6. 설정 파일

### 6.1 server_config.json

```json
{
  "server": {
    "port": 7779,
    "max_bots": 200,
    "update_rate": 30,
    "max_connections": 10
  },
  "logging": {
    "level": "Information",
    "file_path": "logs/bot_controller.log"
  },
  "performance": {
    "max_cpu_percent": 80,
    "max_memory_mb": 4096,
    "tick_warning_ms": 50
  }
}
```

### 6.2 difficulty_settings.json

```json
{
  "difficulties": {
    "easy": {
      "accuracy_multiplier": 0.6,
      "reaction_time_multiplier": 1.5,
      "bot_count": 50
    },
    "normal": {
      "accuracy_multiplier": 1.0,
      "reaction_time_multiplier": 1.0,
      "bot_count": 100
    },
    "hard": {
      "accuracy_multiplier": 1.5,
      "reaction_time_multiplier": 0.7,
      "bot_count": 150
    }
  }
}
```

---

## 7. 체크리스트

```
개발 환경:
  ✓ .NET 8.0 SDK 설치
  ✓ Visual Studio 2022 (또는 VS Code)
  ✓ LiteNetLib NuGet 패키지
  ✓ Serilog 로깅 설정

코어 구현:
  ✓ BotController 메인 루프
  ✓ BotAgent 클래스
  ✓ FSM 상태 머신
  ✓ AccuracySystem
  ✓ SuppressionSystem
  ✓ NetworkManager
  ✓ 패킷 직렬화

테스트:
  ✓ 단위 테스트
  ✓ 통합 테스트
  ✓ 성능 테스트

배포:
  ✓ Release 빌드
  ✓ Docker 이미지
  ✓ 설정 파일
  ✓ 로깅 설정
  ✓ 모니터링 대시보드
```

