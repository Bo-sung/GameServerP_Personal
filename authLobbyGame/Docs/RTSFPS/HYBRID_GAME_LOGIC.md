# FPS/RTS 하이브리드 게임 로직 구현

## 1. 게임 루프 아키텍처

### 1.1 Unreal 게임 모드 메인 루프

```csharp
// C++: AHybridGameMode.h
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "HybridGameMode.generated.h"

UENUM()
enum class EGamePhase : uint8 {
    Warmup = 0,      // 2분 준비
    InProgress = 1,  // 2-20분 실제 게임
    Finished = 2     // 게임 종료
};

UCLASS()
class HYBRIDGAME_API AHybridGameMode : public AGameModeBase {
    GENERATED_BODY()

public:
    AHybridGameMode();
    
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;
    
    // 게임 상태 관리
    UPROPERTY(BlueprintReadOnly)
    EGamePhase CurrentPhase = EGamePhase::Warmup;
    
    UPROPERTY(BlueprintReadOnly)
    float GameTime = 0.0f;
    
    UPROPERTY(BlueprintReadOnly)
    int32 FpsTeamKills = 0;
    
    UPROPERTY(BlueprintReadOnly)
    int32 AliveBotCount = 0;

private:
    float PhaseTimer = 0.0f;
    const float WarmupDuration = 120.0f;  // 2분
    const float MaxGameDuration = 1200.0f; // 20분
    
    void UpdateGamePhase();
    void CheckVictoryConditions();
};
```

### 1.2 게임 루프 흐름

```
┌─────────────────────┐
│   Warmup Phase      │  (0-120초)
│   - 플레이어 로드   │
│   - 봇 스폰        │
│   - 준비 시간      │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│  InProgress Phase   │  (120-1320초)
│  - 게임 실행        │
│  - AI 업데이트      │
│  - 점수 계산       │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│  CheckVictory       │
│  - 모든 봇 제거?    │
│  - 모든 FPS 죽음?   │
│  - 시간 초과?      │
└──────────┬──────────┘
           ↓
┌─────────────────────┐
│  Finished Phase     │  (1320초+)
│  - 결과 저장       │
│  - 보상 배분       │
│  - 통계 기록       │
└─────────────────────┘
```

---

## 2. FPS 클라이언트 게임 로직

### 2.1 플레이어 컨트롤러

```csharp
// C++: AFPSPlayerController.h
class AFPSPlayerController : public APlayerController {
    GENERATED_BODY()

public:
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;
    virtual void SetupInputComponent() override;

private:
    // 입력 처리
    void OnMoveForward(float AxisValue);
    void OnMoveRight(float AxisValue);
    void OnLook(const FInputActionValue& Value);
    void OnFire();
    void OnReload();
    void OnAbilityUsed(ESquadAbility Ability);
    
    // 입력 버퍼
    FVector InputDirection = FVector::ZeroVector;
    bool bIsFiring = false;
    
    // 네트워크 전송 (60Hz)
    FTimerHandle InputReplicationTimer;
    void SendInputToServer();
};

// C++: AFPSPlayerController.cpp
void AFPSPlayerController::SetupInputComponent() {
    Super::SetupInputComponent();
    
    // 이동 입력
    InputComponent->BindAxis("MoveForward", this, &AFPSPlayerController::OnMoveForward);
    InputComponent->BindAxis("MoveRight", this, &AFPSPlayerController::OnMoveRight);
    
    // 마우스 시점
    InputComponent->BindAxis("LookUp", this, &AFPSPlayerController::OnLook);
    InputComponent->BindAxis("Turn", this, &AFPSPlayerController::OnLook);
    
    // 무기 조작
    InputComponent->BindAction("Fire", IE_Pressed, this, &AFPSPlayerController::OnFire);
    InputComponent->BindAction("Reload", IE_Pressed, this, &AFPSPlayerController::OnReload);
}

void AFPSPlayerController::SendInputToServer() {
    if (!GetPawn()) return;
    
    FPlayerInputPacket InputPacket;
    InputPacket.PlayerId = FString::Printf(TEXT("player_%d"), PlayerState->GetPlayerId());
    InputPacket.Position = GetPawn()->GetActorLocation();
    InputPacket.Rotation = GetPawn()->GetActorRotation();
    InputPacket.InputFlags = 0;
    
    if (!InputDirection.IsZero()) {
        InputPacket.InputFlags |= 0x01;  // Forward
    }
    if (InputDirection.Y != 0) {
        InputPacket.InputFlags |= 0x02;  // Right
    }
    if (bIsFiring) {
        InputPacket.InputFlags |= 0x04;  // Fire
    }
    
    // 네트워크로 전송 (UDP, 신뢰성 없음)
    SendInputPacket(InputPacket);
}
```

### 2.2 플레이어 스쿼드 관리

```csharp
// C#: FPSSquadManager.cs
public class FPSSquadManager {
    private List<FPSPlayerCharacter> squadMembers = new();
    private FPSSquadClass[] classes;
    
    public enum FPSSquadClass {
        Leader,      // 생명력 높음, 무기 AR, 특수능력: 그룹 힐
        Medic,       // 지원 특화, 무기 권총, 특수능력: 응급 치료
        Engineer,    // 방어 특화, 무기 샷건, 특수능력: 요새화
        Sniper       // 원거리, 무기 저격총, 특수능력: 벽뚫기
    }
    
    public void UpdateSquadState(float deltaTime) {
        foreach (var player in squadMembers) {
            // 개인 업데이트
            player.UpdateInput(deltaTime);
            player.UpdateCombat(deltaTime);
            
            // 스쿼드 시너지 적용
            ApplySquadBonuses(player);
        }
        
        // 스쿼드 능력 쿨다운 업데이트
        UpdateAbilityCooldowns(deltaTime);
    }
    
    private void ApplySquadBonuses(FPSPlayerCharacter player) {
        // 팀원과의 거리 계산
        float nearbyAllyCount = 0;
        foreach (var ally in squadMembers) {
            if (ally == player) continue;
            float distance = Vector3.Distance(player.Position, ally.Position);
            if (distance < 20f) {  // 20m 이내
                nearbyAllyCount++;
            }
        }
        
        // 근처 아군 수에 따라 방어력 증가
        player.ArmorMultiplier = 1.0f + (nearbyAllyCount * 0.1f);  // 최대 +40%
    }
}
```

---

## 3. RTS 클라이언트 게임 로직

### 3.1 RTS 커맨더 컨트롤러

```csharp
// C#: RTSCommanderController.cs
public class RTSCommanderController : MonoBehaviour {
    private HashSet<int> selectedUnits = new();
    private Vector3 commandTargetPosition;
    
    void Update() {
        HandleSelection();
        HandleCommands();
        UpdateUI();
    }
    
    private void HandleSelection() {
        if (Input.GetMouseButtonDown(0)) {
            // 좌클릭: 유닛 선택
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit)) {
                if (hit.collider.CompareTag("Unit")) {
                    int botId = hit.collider.GetComponent<UnitRepresentation>().BotId;
                    
                    if (Input.GetKey(KeyCode.LeftShift)) {
                        // Shift 클릭: 기존 선택 유지
                        selectedUnits.Add(botId);
                    } else {
                        // 일반 클릭: 기존 선택 해제
                        selectedUnits.Clear();
                        selectedUnits.Add(botId);
                    }
                }
            }
        }
        
        if (Input.GetKeyDown(KeyCode.A)) {
            // A + 클릭: 공격 명령
            if (Input.GetMouseButtonDown(1)) {
                RaycastHit hit;
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit)) {
                    IssueCommand(RTSCommandType.Attack, hit.point);
                }
            }
        }
        
        if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.A)) {
            // 우클릭 (A 누르지 않음): 이동 명령
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit)) {
                IssueCommand(RTSCommandType.Move, hit.point);
            }
        }
    }
    
    private void IssueCommand(RTSCommandType commandType, Vector3 targetPos) {
        if (selectedUnits.Count == 0) return;
        
        var command = new RTSCommand {
            Type = commandType,
            SelectedBotIds = selectedUnits.ToList(),
            TargetPosition = targetPos,
            Timestamp = Time.time
        };
        
        // 로컬 즉시 처리 (낙관적 업데이트)
        ApplyCommandLocally(command);
        
        // 서버로 전송
        NetworkManager.SendCommand(command);
    }
    
    private void ApplyCommandLocally(RTSCommand command) {
        foreach (int botId in command.SelectedBotIds) {
            UnitRepresentation unit = GetUnit(botId);
            unit.SetTargetPosition(command.TargetPosition);
            unit.SetCommandState(command.Type);
        }
    }
}
```

### 3.2 RTS 명령 해석기

```csharp
// C#: RTSCommandInterpreter.cs
public class RTSCommandInterpreter {
    public void ProcessCommand(RTSCommand command) {
        switch (command.Type) {
            case RTSCommandType.Move:
                ProcessMoveCommand(command);
                break;
            case RTSCommandType.Attack:
                ProcessAttackCommand(command);
                break;
            case RTSCommandType.Stop:
                ProcessStopCommand(command);
                break;
        }
    }
    
    private void ProcessMoveCommand(RTSCommand command) {
        // 목표로 이동하는 경로 생성
        foreach (int botId in command.SelectedBotIds) {
            BotAgent bot = GetBot(botId);
            
            // A* 경로 계산
            List<Vector3> path = Pathfinding.FindPath(
                bot.Position,
                command.TargetPosition
            );
            
            bot.SetMovePath(path);
            bot.State = BotState.Patrol;  // 이동 상태
        }
    }
    
    private void ProcessAttackCommand(RTSCommand command) {
        // 목표 지점 주변의 적을 공격
        foreach (int botId in command.SelectedBotIds) {
            BotAgent bot = GetBot(botId);
            
            // 목표 주변 5m 범위의 FPS 플레이어 검색
            Collider[] enemiesInRange = Physics.OverlapSphere(
                command.TargetPosition,
                5f
            );
            
            foreach (var enemy in enemiesInRange) {
                if (enemy.CompareTag("FPSPlayer")) {
                    bot.SetTarget(enemy.gameObject);
                    bot.State = BotState.Combat;
                }
            }
        }
    }
    
    private void ProcessStopCommand(RTSCommand command) {
        foreach (int botId in command.SelectedBotIds) {
            BotAgent bot = GetBot(botId);
            bot.ClearPath();
            bot.State = BotState.Patrol;
        }
    }
}
```

---

## 4. C# 봇 컨트롤러 게임 루프

### 4.1 봇 컨트롤러 메인 서버

```csharp
// C#: BotControllerServer.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class BotControllerServer {
    private const int BotUpdateTickRate = 30;  // 30Hz
    private const float TickDelta = 1f / BotUpdateTickRate;  // 33ms
    
    private Dictionary<int, BotAgent> activeBots = new();
    private ServerSocket unrealSocket;
    private ServerSocket rtsSocket;
    private Stopwatch gameTimer = new();
    
    public async Task Start() {
        unrealSocket = new ServerSocket(7779);  // Unreal 서버와 통신
        rtsSocket = new ServerSocket(7778);     // RTS 클라이언트와 통신
        
        // 봇 업데이트 루프 (높은 주기)
        await StartBotUpdateLoop();
        
        // 네트워크 리스너
        Task.Run(() => ListenForUnrealPackets());
        Task.Run(() => ListenForRTSPackets());
    }
    
    private async Task StartBotUpdateLoop() {
        gameTimer.Start();
        long targetTickMs = (long)(TickDelta * 1000);
        
        while (true) {
            long tickStartMs = gameTimer.ElapsedMilliseconds;
            
            // 1. 모든 봇 업데이트
            foreach (var bot in activeBots.Values) {
                bot.UpdateFSM(TickDelta);
                bot.UpdateMovement(TickDelta);
                bot.UpdateAccuracy();
            }
            
            // 2. Unreal 서버로 봇 상태 전송
            SendBotStatesToUnreal();
            
            // 3. 프레임 타이밍 유지
            long tickElapsedMs = gameTimer.ElapsedMilliseconds - tickStartMs;
            long sleepMs = targetTickMs - tickElapsedMs;
            
            if (sleepMs > 0) {
                await Task.Delay((int)sleepMs);
            }
        }
    }
    
    private void SendBotStatesToUnreal() {
        var batch = new BotStateBatch();
        batch.Timestamp = gameTimer.ElapsedMilliseconds;
        batch.Bots = new List<BotStatePacket>();
        
        foreach (var bot in activeBots.Values) {
            batch.Bots.Add(new BotStatePacket {
                BotId = bot.Id,
                Position = bot.Position,
                Health = bot.Health,
                Suppression = bot.Suppression,
                State = (byte)bot.State,
                InCover = bot.InCover
            });
        }
        
        unrealSocket.SendBatch(batch);
    }
    
    private void ListenForUnrealPackets() {
        while (true) {
            var packet = unrealSocket.ReceivePacket();
            
            if (packet is BotInputPacket botInput) {
                if (activeBots.TryGetValue(botInput.BotId, out var bot)) {
                    bot.ApplyInput(botInput);
                }
            }
        }
    }
    
    private void ListenForRTSPackets() {
        while (true) {
            var packet = rtsSocket.ReceivePacket();
            
            if (packet is RTSCommandPacket command) {
                ProcessRTSCommand(command);
            }
        }
    }
    
    private void ProcessRTSCommand(RTSCommandPacket command) {
        foreach (int botId in command.SelectedBotIds) {
            if (activeBots.TryGetValue(botId, out var bot)) {
                if (command.Type == RTSCommandType.Move) {
                    bot.SetTargetPosition(command.TargetPosition);
                } else if (command.Type == RTSCommandType.Attack) {
                    bot.SetTargetPosition(command.TargetPosition);
                    bot.SetAggressive(true);
                }
            }
        }
    }
}
```

### 4.2 개별 봇 업데이트

```csharp
// C#: BotAgent.cs
public class BotAgent {
    public int Id { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public BotState State { get; set; }
    public float Health { get; set; } = 100f;
    public float Suppression { get; set; }
    public bool InCover { get; set; }
    
    private BotProfile profile;
    private BotFSM fsm;
    private AccuracySystem accuracy;
    private SuppressionSystem suppression;
    
    public void UpdateFSM(float deltaTime) {
        // 1. 입력 처리 (Unreal에서 수신)
        ProcessInput();
        
        // 2. FSM 상태 업데이트
        State = fsm.Update(deltaTime, this);
        
        // 3. 정확도 업데이트
        accuracy.Update(deltaTime);
        
        // 4. 억압 업데이트
        suppression.Update(deltaTime);
        
        // 5. 상태 검증
        ValidateState();
    }
    
    public void UpdateMovement(float deltaTime) {
        // 1. 타겟 위치 업데이트
        if (HasTargetPath) {
            MoveAlongPath(deltaTime);
        }
        
        // 2. 엄폐 효과 적용
        if (InCover) {
            // 엄폐에서 더 천천히 이동
            Velocity *= 0.5f;
        }
        
        // 3. 억압 효과 적용
        float suppressionPenalty = suppression.GetMovementSpeedPenalty();
        Velocity *= (1f - suppressionPenalty);
        
        // 4. 위치 업데이트
        Position += Velocity * deltaTime;
    }
    
    public void UpdateAccuracy() {
        // 거리 기반 정확도 감소
        float distance = Vector3.Distance(Position, CurrentTarget?.Position ?? Vector3.zero);
        accuracy.DistanceFactor = Mathf.Clamp01(1f - (distance / 100f));
        
        // 억압 기반 정확도 감소
        accuracy.SuppressionFactor = 1f - (suppression.Level / 100f);
        
        // 봇 프로필 정확도 보정
        accuracy.BotSkillFactor = profile.AccuracyBonus;
    }
    
    private void ValidateState() {
        // 체력 검증
        if (Health <= 0) {
            State = BotState.Dead;
        }
        
        // 엄폐 위치 검증
        if (InCover && !IsValidCoverPosition(Position)) {
            InCover = false;
        }
    }
}
```

---

## 5. 전투 시스템

### 5.1 히트스캔 총기 처리

```csharp
// C++: AWeapon.cpp
void AWeapon::Fire(APawn* Shooter) {
    // 1. 클라이언트 입력 기록
    FVector FireStart = Shooter->GetActorLocation() + Shooter->GetActorForwardVector() * 100;
    FVector FireDirection = Shooter->GetActorForwardVector();
    
    // 2. 부정확성 추가 (AI의 관찰 시간 기반)
    float Inaccuracy = CalculateInaccuracy(Shooter);
    FireDirection = AddInaccuracyToDirection(FireDirection, Inaccuracy);
    
    // 3. 히트스캔 수행
    FHitResult HitResult;
    FCollisionQueryParams QueryParams;
    QueryParams.AddIgnoredActor(Shooter);
    
    bool bHit = GetWorld()->LineTraceSingleByChannel(
        HitResult,
        FireStart,
        FireStart + FireDirection * 10000.0f,
        ECC_Visibility,
        QueryParams
    );
    
    if (bHit && HitResult.GetActor()) {
        // 4. 피해 적용
        ApplyDamage(HitResult.GetActor(), HitResult.ImpactPoint, Damage);
        
        // 5. 클라이언트에 이펙트 재생
        SpawnImpactEffect(HitResult.ImpactPoint, HitResult.ImpactNormal);
    }
    
    // 6. 네트워크 전송 (RPC)
    MulticastFire(FireStart, FireDirection);
}

float AWeapon::CalculateInaccuracy(APawn* Shooter) {
    ABotController* BotController = Cast<ABotController>(Shooter->Controller);
    if (!BotController) return 0.0f;
    
    // AI 봇의 정확도 계산
    float ObservationTime = BotController->GetObservationTime();
    float Accuracy = 0.25f * Mathf.Pow(ObservationTime / 5f, 0.5f);  // 0-100% 범위
    
    // 정확도에서 부정확성으로 변환
    return (1.0f - Accuracy) * MaxInaccuracy;
}
```

### 5.2 피해 및 상태 이상

```csharp
// C#: DamageSystem.cs
public class DamageSystem {
    public static void ApplyDamage(BotAgent bot, Vector3 impactPoint, float damage) {
        // 1. 헤드샷 판정
        bool isHeadshot = IsHeadshotHit(bot, impactPoint);
        if (isHeadshot) {
            damage *= 2.0f;  // 헤드샷 2배 피해
        }
        
        // 2. 체력 감소
        bot.Health -= damage;
        
        // 3. 상태 이상 적용
        if (bot.Health <= 0) {
            KillBot(bot);
        } else {
            // 피해 반응
            bot.ReactToDamage(impactPoint, damage);
        }
        
        // 4. 억압 증가
        float suppressionIncrease = damage * 0.3f;  // 피해의 30%
        bot.Suppression += suppressionIncrease;
        bot.Suppression = Mathf.Min(bot.Suppression, 100f);
    }
    
    private static bool IsHeadshotHit(BotAgent bot, Vector3 impactPoint) {
        // 봇의 머리 위치 범위 (0.5m 높이, 0.2m 반경)
        Vector3 headCenter = bot.Position + Vector3.up * 1.7f;
        float distanceToHead = Vector3.Distance(impactPoint, headCenter);
        
        return distanceToHead < 0.25f;  // 0.25m 범위 = 헤드샷
    }
    
    private static void KillBot(BotAgent bot) {
        bot.State = BotState.Dead;
        bot.Health = 0;
        
        // 사망 이벤트 브로드캐스트
        GameEventSystem.BroadcastEvent(new BotDeathEvent {
            BotId = bot.Id,
            KillerPlayerId = GetLastAttacker(bot),
            Position = bot.Position
        });
    }
}
```

---

## 6. 게임 이벤트 시스템

### 6.1 이벤트 정의

```csharp
// C#: GameEvents.cs
public interface IGameEvent {
    ulong GetEventHash();
    void Serialize(ByteBuffer buffer);
}

public class BotDeathEvent : IGameEvent {
    public int BotId { get; set; }
    public string KillerPlayerId { get; set; }
    public Vector3 Position { get; set; }
    
    public ulong GetEventHash() {
        return ((ulong)BotId << 32) | ((ulong)KillerPlayerId.GetHashCode() & 0xFFFFFFFF);
    }
}

public class PlayerKilledEvent : IGameEvent {
    public string PlayerId { get; set; }
    public int KillerBotId { get; set; }
    
    public ulong GetEventHash() {
        return ((ulong)PlayerId.GetHashCode() << 32) | ((ulong)KillerBotId & 0xFFFFFFFF);
    }
}

public class GameEndEvent : IGameEvent {
    public string WinningTeam { get; set; }  // FPS or RTS
    public Dictionary<string, int> Scores { get; set; }
    
    public ulong GetEventHash() {
        return 0x0000DEAD;  // 특수 해시
    }
}
```

### 6.2 이벤트 브로드캐스트

```csharp
// C#: GameEventBroadcaster.cs
public class GameEventBroadcaster {
    private Queue<IGameEvent> eventQueue = new();
    private HashSet<ulong> sentEventHashes = new();  // 중복 제거
    
    public void BroadcastEvent(IGameEvent evt) {
        // 중복 검사
        ulong hash = evt.GetEventHash();
        if (sentEventHashes.Contains(hash)) {
            return;  // 이미 전송됨
        }
        sentEventHashes.Add(hash);
        
        // 이벤트 큐에 추가
        eventQueue.Enqueue(evt);
    }
    
    public void FlushEvents() {
        while (eventQueue.Count > 0) {
            IGameEvent evt = eventQueue.Dequeue();
            
            // TCP로 신뢰성 있게 전송
            var packet = new EventPacket {
                EventData = Serialize(evt),
                Timestamp = DateTime.UtcNow.Ticks
            };
            
            // 모든 클라이언트로 전송
            BroadcastToAllClients(packet);
        }
        
        // 1초마다 중복 해시 초기화
        sentEventHashes.Clear();
    }
}
```

---

## 7. 점수 계산 시스템

### 7.1 FPS 팀 점수

```csharp
// C#: ScoringSystem.cs
public class ScoringSystem {
    private const int KillPoints = 10;
    private const int HeadshotBonus = 5;
    private const int AssistPoints = 5;
    private const int SurvivalBonus = 1;  // 초마다
    
    public int CalculateFPSPlayerScore(FPSPlayerStats stats) {
        int score = 0;
        
        // 처치 점수
        score += stats.Kills * KillPoints;
        score += stats.Headshots * HeadshotBonus;
        
        // 어시스트 점수
        score += stats.Assists * AssistPoints;
        
        // 생존 보너스 (20분 생존 = 1200점)
        score += stats.TimeSurvivedSeconds / 60;
        
        // 능력 사용 보너스
        score += (stats.AbilitiesUsed * 20);
        
        return score;
    }
    
    public int CalculateRTSCommanderScore(RTSStats stats) {
        int score = 0;
        
        // 봇 처치 점수
        score += stats.UnitsKilled * 5;
        
        // FPS 팀 처치 유도
        score += stats.FPSPlayersEliminatedByUnits * 50;
        
        // 맵 제어 점수
        score += (int)(stats.MapCoveragePercent * 100);
        
        // 명령 효율
        score += (int)(stats.CommandAccuracy * 100);
        
        return score;
    }
}
```

---

## 8. 게임 종료 조건

### 8.1 승리 조건 검사

```csharp
// C#: VictoryConditionChecker.cs
public class VictoryConditionChecker {
    public GameResult CheckVictoryConditions(GameState state) {
        // 조건 1: 모든 봇 제거 (FPS 승리)
        if (state.AliveBotCount == 0) {
            return new GameResult {
                Winner = Team.FPS,
                Reason = "All bots eliminated",
                Duration = state.GameDuration
            };
        }
        
        // 조건 2: 모든 FPS 플레이어 사망 (RTS 승리)
        if (state.AliveFPSPlayers == 0) {
            return new GameResult {
                Winner = Team.RTS,
                Reason = "All FPS players eliminated",
                Duration = state.GameDuration
            };
        }
        
        // 조건 3: 시간 초과 (20분) - 봇 수로 판단
        if (state.GameDuration >= 1200) {
            int fpsBotKills = state.TotalBotsSpawned - state.AliveBotCount;
            float killRatio = fpsBotKills / (float)state.TotalBotsSpawned;
            
            if (killRatio > 0.5f) {
                // FPS가 절반 이상 처치
                return new GameResult {
                    Winner = Team.FPS,
                    Reason = "Time limit reached - FPS won by kill ratio",
                    Duration = state.GameDuration
                };
            } else {
                // RTS가 더 많은 봇 유지
                return new GameResult {
                    Winner = Team.RTS,
                    Reason = "Time limit reached - RTS won by bot count",
                    Duration = state.GameDuration
                };
            }
        }
        
        return null;  // 게임 진행 중
    }
}
```

---

## 9. 게임 로직 체크리스트

```
FPS 클라이언트:
  ✓ 입력 수집 (이동, 마우스, 발사)
  ✓ 클라이언트 예측
  ✓ 60Hz 서버 전송
  ✓ 총기 발사 처리
  ✓ 데미지 수신

RTS 클라이언트:
  ✓ 유닛 선택 시스템
  ✓ 명령 인터페이스 (이동/공격)
  ✓ 10Hz 상태 업데이트
  ✓ 명령 신뢰성 보장

Unreal 서버:
  ✓ 게임 루프 (게임모드)
  ✓ 플레이어 입력 처리
  ✓ 히트스캔 총기 처리
  ✓ 30Hz 봇 상태 송신
  ✓ 승리 조건 확인

C# 봇 컨트롤러:
  ✓ 30Hz 봇 업데이트
  ✓ FSM 상태 전환
  ✓ 정확도 계산
  ✓ 억압 시스템
  ✓ RTS 명령 처리
  ✓ 이동 경로 계산

게임 시스템:
  ✓ 점수 계산
  ✓ 이벤트 브로드캐스트
  ✓ 게임 종료 조건
  ✓ 데이터베이스 저장
```

