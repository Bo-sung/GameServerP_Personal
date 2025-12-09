# FPS/RTS 하이브리드 게임 테스팅 계획

## 1. 테스트 피라미드

```
┌─────────────────────┐
│   E2E 테스트 (5%)   │  완전한 게임 플레이
├─────────────────────┤
│ 통합 테스트 (15%)   │  서버 간 통신, 동기화
├─────────────────────┤
│  단위 테스트 (80%)  │  개별 컴포넌트
└─────────────────────┘
```

---

## 2. 단위 테스트

### 2.1 AI FSM 테스트

```csharp
// C#: BotFSMTests.cs
[TestClass]
public class BotFSMTests {
    [TestMethod]
    public void TestPatrolState_WhenThreatDetected_TransitionsToInvestigate() {
        // Arrange
        var bot = new BotAgent { State = BotState.Patrol };
        var threat = new Vector3(10, 0, 0);  // 10m 거리
        
        // Act
        bot.DetectThreat(threat);
        bot.UpdateFSM(0.016f);  // 1프레임 (60Hz)
        
        // Assert
        Assert.AreEqual(BotState.Investigate, bot.State);
    }
    
    [TestMethod]
    public void TestCombatState_WhenTargetOutOfRange_RetreatsOrPatrols() {
        // Arrange
        var bot = new BotAgent { State = BotState.Combat, Health = 30f };
        bot.SetTarget(null);  // 타겟 상실
        
        // Act
        bot.UpdateFSM(0.016f);
        
        // Assert
        Assert.IsTrue(bot.State == BotState.TacticalRetreat || bot.State == BotState.Patrol);
    }
    
    [TestMethod]
    public void TestRetreatState_InCover_StaysInRetreat() {
        // Arrange
        var bot = new BotAgent { 
            State = BotState.TacticalRetreat, 
            InCover = true,
            Health = 25f
        };
        
        // Act
        bot.UpdateFSM(0.016f);
        
        // Assert
        Assert.AreEqual(BotState.TacticalRetreat, bot.State);
    }
}
```

### 2.2 정확도 시스템 테스트

```csharp
[TestClass]
public class AccuracySystemTests {
    [TestMethod]
    public void TestAccuracy_AtObservationTime0_Returns10Percent() {
        // Arrange
        var accuracy = new AccuracySystem();
        var bot = new BotAgent { ObservationTime = 0f };
        
        // Act
        float result = accuracy.CalculateAccuracy(bot);
        
        // Assert
        Assert.AreEqual(0.10f, result, 0.01f);
    }
    
    [TestMethod]
    public void TestAccuracy_AtObservationTime5_Returns50Percent() {
        // Arrange
        var accuracy = new AccuracySystem();
        var bot = new BotAgent { ObservationTime = 5f };
        
        // Act
        float result = accuracy.CalculateAccuracy(bot);
        
        // Assert
        Assert.AreEqual(0.50f, result, 0.01f);
    }
    
    [TestMethod]
    [DataRow(10f, 0.75f)]
    [DataRow(20f, 0.80f)]  // 수렴
    public void TestAccuracy_Curves(float observationTime, float expectedAccuracy) {
        var accuracy = new AccuracySystem();
        var bot = new BotAgent { ObservationTime = observationTime };
        
        float result = accuracy.CalculateAccuracy(bot);
        
        Assert.AreEqual(expectedAccuracy, result, 0.05f);
    }
}
```

### 2.3 억압 시스템 테스트

```csharp
[TestClass]
public class SuppressionSystemTests {
    [TestMethod]
    public void TestSuppression_TakingFire_Increases() {
        // Arrange
        var suppression = new SuppressionSystem();
        var bot = new BotAgent();
        float initialSuppression = bot.Suppression;
        
        // Act - 0.5초간 집중 사격
        for (int i = 0; i < 30; i++) {
            bot.TakeFire();
            suppression.Update(0.016f);
        }
        
        // Assert
        Assert.IsTrue(bot.Suppression > initialSuppression + 10f);
    }
    
    [TestMethod]
    public void TestSuppression_InCover_DecaysSlower() {
        // Arrange
        var bot1 = new BotAgent { Suppression = 80f, InCover = false };
        var bot2 = new BotAgent { Suppression = 80f, InCover = true };
        var system = new SuppressionSystem();
        
        // Act - 1초 경과
        for (int i = 0; i < 60; i++) {
            system.Update(0.016f);
        }
        
        // Assert
        Assert.IsTrue(bot2.Suppression > bot1.Suppression);  // 엄폐한 봇이 더 높음
    }
}
```

### 2.4 경로 계산 테스트

```csharp
[TestClass]
public class PathfindingTests {
    [TestMethod]
    public void TestPathfinding_SimpleRoute_FindsPath() {
        // Arrange
        var nav = new Pathfinding(gridSize: 50);
        Vector3 start = new Vector3(0, 0, 0);
        Vector3 goal = new Vector3(10, 0, 10);
        
        // Act
        var path = nav.FindPath(start, goal);
        
        // Assert
        Assert.IsNotNull(path);
        Assert.IsTrue(path.Count > 0);
        Assert.AreEqual(goal, path[path.Count - 1]);
    }
    
    [TestMethod]
    public void TestPathfinding_Obstacle_AvoidesIt() {
        // Arrange
        var nav = new Pathfinding(gridSize: 50);
        nav.AddObstacle(new Vector3(5, 0, 5), radius: 2f);
        
        // Act
        var path = nav.FindPath(
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 10)
        );
        
        // Assert
        Assert.IsNotNull(path);
        foreach (var waypoint in path) {
            float distToObstacle = Vector3.Distance(waypoint, new Vector3(5, 0, 5));
            Assert.IsTrue(distToObstacle > 2.5f);  // 충돌 안 함
        }
    }
}
```

---

## 3. 통합 테스트

### 3.1 네트워크 통신 테스트

```csharp
// C#: NetworkIntegrationTests.cs
[TestClass]
public class NetworkIntegrationTests {
    private BotControllerServer botServer;
    private MockUnrealServer unrealMock;
    private MockRTSClient rtsMock;
    
    [TestInitialize]
    public void Setup() {
        botServer = new BotControllerServer();
        unrealMock = new MockUnrealServer();
        rtsMock = new MockRTSClient();
    }
    
    [TestMethod]
    public async Task TestBotStateSync_Unreal_ReceivesBotUpdates() {
        // Arrange
        await botServer.Start();
        var bot = botServer.SpawnBot(1);
        bot.Position = new Vector3(100, 0, 50);
        bot.Health = 85;
        
        // Act - 다음 업데이트 대기
        await Task.Delay(50);  // 30Hz = 33ms
        var received = unrealMock.GetLastBotStatePacket();
        
        // Assert
        Assert.IsNotNull(received);
        Assert.AreEqual(1, received.BotId);
        Assert.AreEqual(new Vector3(100, 0, 50), received.Position);
        Assert.AreEqual(85, received.Health);
    }
    
    [TestMethod]
    public async Task TestRTSCommand_Processed_BotMovesToTarget() {
        // Arrange
        await botServer.Start();
        var bot = botServer.SpawnBot(1);
        var command = new RTSCommandPacket {
            Type = RTSCommandType.Move,
            SelectedBotIds = new[] { 1 },
            TargetPosition = new Vector3(200, 0, 100)
        };
        
        // Act
        rtsMock.SendCommand(command);
        await Task.Delay(100);  // 명령 처리 대기
        
        // Assert
        Assert.IsTrue(bot.HasMovementTarget);
        Assert.AreEqual(new Vector3(200, 0, 100), bot.TargetPosition);
    }
    
    [TestMethod]
    public async Task TestLagCompensation_PlayerFires_HitsMovingBot() {
        // Arrange
        const float RTT = 0.1f;  // 100ms RTT
        var bot = botServer.SpawnBot(1);
        bot.Position = new Vector3(50, 1.7f, 0);  // 헤드 높이
        
        // Bot이 이동 중
        bot.Velocity = new Vector3(10, 0, 0);  // 매초 10m
        
        // Act - RTT 시간 전 위치에서 발사
        Vector3 pastBotPos = bot.Position - bot.Velocity * RTT;
        bool hit = botServer.HitscanTest(
            firePosition: new Vector3(0, 1.7f, 0),
            fireDirection: (pastBotPos - new Vector3(0, 1.7f, 0)).normalized,
            tolerance: 0.5f
        );
        
        // Assert
        Assert.IsTrue(hit);  // 과거 위치 계산으로 명중
    }
}
```

### 3.2 동기화 일관성 테스트

```csharp
[TestClass]
public class SynchronizationTests {
    [TestMethod]
    public async Task TestGameState_AllClients_Consistent() {
        // Arrange
        var server = new BotControllerServer();
        var client1 = new FPSClientSimulator();
        var client2 = new FPSClientSimulator();
        var rtsClient = new RTSClientSimulator();
        
        await server.Start();
        client1.Connect(server);
        client2.Connect(server);
        rtsClient.Connect(server);
        
        // Act - 10초 게임 실행
        server.SpawnBots(100);
        await Task.Delay(10000);
        
        // Assert - 모든 클라이언트의 봇 위치 일치
        var client1State = client1.GetGameState();
        var client2State = client2.GetGameState();
        var rtsState = rtsClient.GetGameState();
        
        foreach (int botId in Enumerable.Range(1, 100)) {
            Assert.AreEqual(
                client1State.GetBotPosition(botId),
                client2State.GetBotPosition(botId),
                tolerance: 1.0f
            );
        }
    }
}
```

---

## 4. 성능 테스트

### 4.1 로드 테스트

```csharp
// C#: PerformanceTests.cs
[TestClass]
public class PerformanceTests {
    [TestMethod]
    public void TestBotController_100Bots_CPUUsage() {
        // Arrange
        var botController = new BotControllerServer();
        botController.SpawnBots(100);
        var cpuMonitor = new CPUMonitor();
        
        // Act
        cpuMonitor.Start();
        for (int i = 0; i < 3600; i++) {  // 2분 (30Hz × 120s)
            botController.Update(0.0333f);
        }
        var cpuUsage = cpuMonitor.GetAverageCPUPercent();
        
        // Assert
        Assert.IsTrue(cpuUsage < 80, $"CPU usage {cpuUsage}% exceeds 80% threshold");
    }
    
    [TestMethod]
    public void TestMemoryUsage_100Bots() {
        // Arrange
        var botController = new BotControllerServer();
        long initialMemory = GC.GetTotalMemory(true);
        
        // Act
        botController.SpawnBots(100);
        long finalMemory = GC.GetTotalMemory(false);
        long usedMemory = (finalMemory - initialMemory) / (1024 * 1024);  // MB
        
        // Assert
        Assert.IsTrue(usedMemory < 500, $"Memory usage {usedMemory}MB exceeds 500MB");
    }
    
    [TestMethod]
    public void TestNetworkBandwidth_100Bots() {
        // Arrange
        var botController = new BotControllerServer();
        botController.SpawnBots(100);
        var networkMonitor = new NetworkMonitor();
        
        // Act
        networkMonitor.Start();
        for (int i = 0; i < 300; i++) {  // 10초 (30Hz)
            botController.Update(0.0333f);
        }
        var bandwidthKBps = networkMonitor.GetAverageBandwidth() / 1024;
        
        // Assert
        Assert.IsTrue(bandwidthKBps < 150, $"Bandwidth {bandwidthKBps} KB/s exceeds 150 KB/s");
    }
}
```

### 4.2 확장성 테스트

```csharp
[TestClass]
public class ScalabilityTests {
    [TestMethod]
    [DataRow(50)]
    [DataRow(100)]
    [DataRow(150)]
    [DataRow(200)]
    public void TestBotCount_Scalability(int botCount) {
        // Arrange
        var botController = new BotControllerServer();
        var stats = new PerformanceStats();
        
        // Act
        botController.SpawnBots(botCount);
        
        // 30초 실행
        for (int frame = 0; frame < 900; frame++) {
            var frameStat = new FrameStat {
                FrameTime = MeasureFrameTime(() => botController.Update(0.0333f)),
                BotCount = botCount
            };
            stats.AddFrame(frameStat);
        }
        
        // Assert
        float avgFrameTime = stats.GetAverageFrameTime();
        float targetFrameTime = 33.3f;  // 30Hz
        
        // 프레임 시간이 대상의 1.5배 이하
        Assert.IsTrue(avgFrameTime < targetFrameTime * 1.5f,
            $"{botCount} bots: {avgFrameTime:F1}ms exceeds target {targetFrameTime * 1.5f:F1}ms");
    }
}
```

---

## 5. 밸런스 테스트

### 5.1 승률 테스트

```csharp
// C#: BalanceTests.cs
[TestClass]
public class BalanceTests {
    [TestMethod]
    [DataRow(Difficulty.Easy)]
    [DataRow(Difficulty.Normal)]
    [DataRow(Difficulty.Hard)]
    public async Task TestDifficulty_FPSWinRate_WithinRange(Difficulty difficulty) {
        // Arrange
        const int NumGames = 20;
        const float TargetFPSWinRate = 0.5f;
        const float Tolerance = 0.15f;  // ±15%
        
        int fpsWins = 0;
        
        // Act
        for (int i = 0; i < NumGames; i++) {
            var game = new HybridGameSimulator(difficulty);
            var result = await game.PlayToCompletion();
            if (result.Winner == Team.FPS) {
                fpsWins++;
            }
        }
        
        float actualWinRate = (float)fpsWins / NumGames;
        
        // Assert
        Assert.IsTrue(
            actualWinRate >= TargetFPSWinRate - Tolerance &&
            actualWinRate <= TargetFPSWinRate + Tolerance,
            $"Difficulty {difficulty}: FPS win rate {actualWinRate:P1} outside target {TargetFPSWinRate:P1} ±{Tolerance:P1}"
        );
    }
    
    [TestMethod]
    public async Task TestWeaponBalance_AllWeapons_Similar_KillRates() {
        // Arrange
        const int GamesPerWeapon = 10;
        var weapons = new[] { Weapon.AR, Weapon.SMG, Weapon.SR, Weapon.SG, Weapon.Pistol };
        var killRates = new Dictionary<Weapon, float>();
        
        // Act
        foreach (var weapon in weapons) {
            int totalKills = 0;
            
            for (int i = 0; i < GamesPerWeapon; i++) {
                var game = new HybridGameSimulator(Difficulty.Normal, forceWeapon: weapon);
                var result = await game.PlayToCompletion();
                totalKills += result.KillsWithWeapon;
            }
            
            killRates[weapon] = totalKills / (float)GamesPerWeapon;
        }
        
        // Assert - 모든 무기의 킬률이 25% 이내 범위
        float maxKillRate = killRates.Values.Max();
        float minKillRate = killRates.Values.Min();
        float variance = (maxKillRate - minKillRate) / minKillRate;
        
        Assert.IsTrue(variance < 0.25f,
            $"Weapon imbalance: max {maxKillRate:F1}, min {minKillRate:F1}, variance {variance:P1}");
    }
}
```

---

## 6. E2E 테스트

### 6.1 완전한 게임 플레이 테스트

```csharp
// C#: E2ETests.cs
[TestClass]
public class EndToEndTests {
    [TestMethod]
    public async Task TestFullGame_FPS_WinByBotElimination() {
        // Arrange
        var gameSession = new GameSessionSimulator();
        gameSession.SetDifficulty(Difficulty.Easy);
        
        // Act - 게임 시작부터 종료까지
        gameSession.Start();
        
        // FPS 플레이어가 모든 봇 제거
        var fps1 = gameSession.GetFPSPlayer(1);
        while (gameSession.AliveBotCount > 0) {
            gameSession.Update(0.016f);
            
            // AI가 매 10프레임마다 봇 하나씩 처치하도록 시뮬레이션
            if (gameSession.FrameCount % 10 == 0) {
                gameSession.KillRandomBot();
            }
        }
        
        var result = gameSession.GetGameResult();
        
        // Assert
        Assert.AreEqual(Team.FPS, result.Winner);
        Assert.AreEqual("All bots eliminated", result.Reason);
        Assert.IsTrue(result.Duration < 300);  // 5분 이내
    }
    
    [TestMethod]
    public async Task TestFullGame_RTS_WinByPlayerElimination() {
        // Arrange
        var gameSession = new GameSessionSimulator();
        gameSession.SetDifficulty(Difficulty.Hard);
        
        // Act
        gameSession.Start();
        
        // RTS가 모든 FPS 플레이어 제거
        while (gameSession.AliveFPSPlayers > 0) {
            gameSession.Update(0.016f);
            
            // 매 5초마다 한 명씩 제거
            if (gameSession.FrameCount % 300 == 0) {
                gameSession.KillRandomFPSPlayer();
            }
        }
        
        var result = gameSession.GetGameResult();
        
        // Assert
        Assert.AreEqual(Team.RTS, result.Winner);
    }
}
```

---

## 7. 테스트 실행 자동화

### 7.1 CI/CD 파이프라인

```yaml
# .github/workflows/tests.yml
name: Automated Testing

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Unit Tests
        run: |
          dotnet test HybridGame.Tests.csproj `
            --configuration Release `
            --logger "console;verbosity=detailed" `
            --filter "Category=Unit"

  integration-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start Mock Servers
        run: |
          Start-Process -FilePath "MockServers/UnrealMock.exe"
          Start-Process -FilePath "MockServers/RTSMock.exe"
          Start-Sleep -Seconds 2
      - name: Run Integration Tests
        run: |
          dotnet test HybridGame.Tests.csproj `
            --configuration Release `
            --filter "Category=Integration"

  performance-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Performance Tests
        run: |
          dotnet test HybridGame.Tests.csproj `
            --configuration Release `
            --filter "Category=Performance" `
            --logger "trx"
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: performance-results
          path: TestResults/
```

---

## 8. 테스트 커버리지 목표

```
Target Coverage: 80%+

| Component | Target | Status |
|-----------|--------|--------|
| FSM Logic | 95% | ✓ |
| Accuracy System | 90% | ✓ |
| Suppression | 85% | ✓ |
| Pathfinding | 80% | ✓ |
| Network Code | 75% | ⚠ |
| Game Logic | 70% | ⚠ |
```

---

## 9. 테스트 체크리스트

```
Unit Tests:
  ✓ AI FSM 상태 전환
  ✓ 정확도 계산
  ✓ 억압 시스템
  ✓ 경로 찾기
  ✓ 점수 계산
  ✓ 데이터 검증

Integration Tests:
  ✓ 네트워크 통신
  ✓ 동기화 일관성
  ✓ 라그 보상
  ✓ 이벤트 브로드캐스트
  ✓ 명령 처리

Performance Tests:
  ✓ 100 봇 CPU 사용률
  ✓ 메모리 사용량
  ✓ 네트워크 대역폭
  ✓ 프레임 타이밍

Balance Tests:
  ✓ 난이도별 승률
  ✓ 무기 밸런스
  ✓ 클래스 성능

E2E Tests:
  ✓ 완전한 게임 플레이
  ✓ 다양한 승리 조건
  ✓ 장시간 안정성
```

