# Bot AI 시스템 (BOT_AI_SYSTEM)

## 1. Bot AI 개요

### 1.1 AI 특징
```
컨셉: 타르코프 스캐브 스타일 AI
• 반응형 (Reactive) - 정해진 경로 아님
• 자율적 (Autonomous) - 명령 없어도 행동
• 현실적 (Realistic) - 인간처럼 실수함
• 개별적 (Individual) - 각 Bot별 특성 다름
```

### 1.2 구현 방식
```
아키텍처: FSM (Finite State Machine) + BehaviorTree
프레임워크: C# .NET 8.0
업데이트 빈도: 30Hz (33ms)
개수: 100개 동시 관리
평균 CPU: <50% (멀티코어)
```

---

## 2. 상태 머신 (FSM)

### 2.1 상태 종류

```csharp
public enum BotState {
    Patrol = 0,           // 순찰 상태
    Investigate = 1,      // 조사 상태
    Combat = 2,           // 전투 상태
    TacticalRetreat = 3,  // 전술 후퇴
    Healing = 4,          // 회복 상태
    Dead = 5              // 사망
}
```

### 2.2 상태 전이도 (State Diagram)

```
┌─────────────┐
│   Patrol    │ ← 시작 상태
└──────┬──────┘
       │ (시체/총소리 감지)
       ↓
┌──────────────┐
│ Investigate  │ ← 3초 동안 그 방향 조사
└──────┬───────┘
       │ (위협 발견 또는 시간 만료)
       ├─→ Patrol (위협 없음)
       └─→ Combat (적 발견)

┌────────────────┐
│    Combat      │ ← 교전 중
└──────┬─────────┘
       │
       ├─→ TacticalRetreat (체력 < 30%)
       ├─→ TacticalRetreat (제압 90%+)
       ├─→ Healing (엄폐 도달 후)
       └─→ Patrol (적 모두 제거)

┌──────────────────┐
│ TacticalRetreat  │ ← 후퇴 중
└──────┬───────────┘
       │
       ├─→ Healing (안전 지역 도달)
       ├─→ Combat (적 추격 중)
       └─→ Patrol (너무 오래 후퇴)

┌────────────┐
│  Healing   │ ← 자가 회복
└──────┬─────┘
       │ (회복 완료 또는 피격)
       ├─→ Patrol (정상)
       └─→ Combat (공격 받음)

┌─────────┐
│  Dead   │ ← 최종 상태
└─────────┘
```

### 2.3 상태별 행동 로직

#### Patrol (순찰)
```csharp
public class PatrolState : BotState {
    private Vector3 targetWaypoint;
    private float patrolTimer = 0;
    private const float PATROL_INTERVAL = 5.0f;

    public override void Enter() {
        SelectRandomWaypoint();
    }

    public override void Update(float deltaTime) {
        // 목표 지점으로 이동
        MoveToward(targetWaypoint, 3.0f);

        // 범위 내 도달 확인
        if (Vector3.Distance(Position, targetWaypoint) < 5.0f) {
            SelectRandomWaypoint();
        }

        // 위협 감지
        if (DetectThreat()) {
            TransitionTo(BotState.Investigate);
        }
    }

    void SelectRandomWaypoint() {
        // 맵 내 랜덤 위치 선택
        targetWaypoint = GetRandomWaypoint();
    }

    bool DetectThreat() {
        // 총소리 감지: 반경 100m 내
        // 시체 감지: 반경 50m 내
        // 시각 감지: 없음 (순찰 중)
        return AudioManager.HasRecentGunfire() ||
               EnvironmentManager.HasCorpsesNearby();
    }
}
```

#### Investigate (조사)
```csharp
public class InvestigateState : BotState {
    private Vector3 investigatePoint;
    private float investigateTimer = 0;
    private const float INVESTIGATE_DURATION = 3.0f;

    public override void Enter(Vector3 threatLocation) {
        investigatePoint = threatLocation;
        investigateTimer = 0;
    }

    public override void Update(float deltaTime) {
        investigateTimer += deltaTime;

        // 위협 지점으로 이동 (느림)
        MoveToward(investigatePoint, 2.0f);

        // 시야에서 적 발견
        int enemyID = FindEnemyInSight();
        if (enemyID >= 0) {
            TransitionTo(BotState.Combat);
            SetTarget(enemyID);
            return;
        }

        // 시간 만료
        if (investigateTimer >= INVESTIGATE_DURATION) {
            TransitionTo(BotState.Patrol);
        }
    }
}
```

#### Combat (전투)
```csharp
public class CombatState : BotState {
    private int targetID = -1;
    private float targetObservationTime = 0;
    private float lastFlankTime = 0;
    private const float FLANK_INTERVAL = 3.0f;

    public override void Update(float deltaTime) {
        // 목표 업데이트
        UpdateTarget(deltaTime);

        if (targetID < 0) {
            // 새 목표 찾기
            targetID = FindEnemyInSight();
            if (targetID < 0) {
                TransitionTo(BotState.Patrol);
                return;
            }
        }

        // 시야에서 목표 확인
        if (!CanSeeTarget(targetID)) {
            targetObservationTime = 0;
            targetID = -1;
            return;
        }

        // 관찰 시간 누적
        targetObservationTime += deltaTime;

        // 명중률 기반 사격
        float accuracy = CalculateAccuracy(targetObservationTime);
        if (Random.value < accuracy) {
            FireAtTarget(targetID);
        }

        // 측면 공격 AI (3초마다)
        lastFlankTime += deltaTime;
        if (lastFlankTime > FLANK_INTERVAL) {
            AttemptFlank();
            lastFlankTime = 0;
        }

        // 후퇴 조건 확인
        if (Health < 30) {
            TransitionTo(BotState.TacticalRetreat);
        }
    }

    float CalculateAccuracy(float observationTime) {
        return observationTime switch {
            < 1f => 0.10f,   // 10% - 패닉
            < 3f => 0.25f,   // 25% - 적 파악
            < 5f => 0.50f,   // 50% - 조준 개선
            _ => 0.75f       // 75% - 완전 집중
        };
    }

    void AttemptFlank() {
        Vector3 targetPos = GetTargetPosition(targetID);
        Vector3 myPos = Position;

        // 측면 공격 지점 계산
        Vector3 flankDirection = CalculateFlankDirection(myPos, targetPos);
        Vector3 flankTarget = targetPos + flankDirection * 20;

        // 경로 찾기
        List<Vector3> path = Pathfinder.FindPath(myPos, flankTarget);
        if (path.Count > 0) {
            MoveAlongPath(path, 3.0f);
        }
    }
}
```

#### TacticalRetreat (전술 후퇴)
```csharp
public class TacticalRetreatState : BotState {
    private Vector3 retreatPoint;
    private float retreatTimer = 0;
    private const float MAX_RETREAT_TIME = 15.0f;

    public override void Enter() {
        // 엄폐물 찾기
        retreatPoint = FindCoverNearby();
        retreatTimer = 0;
    }

    public override void Update(float deltaTime) {
        retreatTimer += deltaTime;

        // 엄폐물로 이동 (느리고 방어적)
        MoveTowardCover(retreatPoint, 2.0f);

        // 범위 내 도달
        if (Vector3.Distance(Position, retreatPoint) < 3.0f) {
            // 엄폐 상태로 전환
            SetInCover(true);
        }

        // 적이 추격 중인가?
        int enemy = FindEnemyInSight();
        if (enemy >= 0) {
            // 약한 반격 (명중률 낮음)
            if (Random.value < 0.15f) {
                FireAtTarget(enemy);
            }
        }

        // 체력 회복 또는 타임아웃
        if (Health > 60 || retreatTimer > MAX_RETREAT_TIME) {
            TransitionTo(BotState.Healing);
        }
    }

    Vector3 FindCoverNearby() {
        // 주변 엄폐물 검색
        // 현재 위치 중심 반경 30m 내에서 찾기
        return CoverSystem.FindBestCover(Position, searchRadius: 30);
    }
}
```

#### Healing (회복)
```csharp
public class HealingState : BotState {
    private float healingTimer = 0;
    private const float HEALING_RATE = 2.0f; // HP/sec
    private const float MAX_HEALING_TIME = 60.0f;

    public override void Update(float deltaTime) {
        healingTimer += deltaTime;

        // 자가 회복 (초당 2 HP)
        Health += HEALING_RATE * deltaTime;
        Health = Math.Min(Health, 100);

        // 피격 받음 - 즉시 전투로
        if (IsUnderFire()) {
            TransitionTo(BotState.Combat);
            return;
        }

        // 회복 완료 또는 타임아웃
        if (Health >= 80 || healingTimer > MAX_HEALING_TIME) {
            TransitionTo(BotState.Patrol);
        }
    }

    bool IsUnderFire() {
        // 최근 1초 내 피격 여부
        return LastDamageTime > (CurrentTime - 1.0f);
    }
}
```

---

## 3. 명중률 시스템

### 3.1 명중률 계산

```csharp
public class AccuracySystem {
    // 명중률 = 기본 명중률 × 상황 보정
    
    public float CalculateAccuracy(
        BotAgent bot,
        float observationTime,
        bool isMoving,
        float distance,
        int suppressionLevel
    ) {
        // 1. 기본 명중률 (관찰 시간)
        float baseAccuracy = GetObservationAccuracy(observationTime);

        // 2. 이동 중 명중률 감소
        float movingModifier = isMoving ? 0.7f : 1.0f;

        // 3. 거리 보정
        float distanceModifier = GetDistanceModifier(distance);

        // 4. 제압 보정
        float suppressionModifier = GetSuppressionModifier(suppressionLevel);

        // 5. Bot 개별 특성
        float skillModifier = bot.AccuracyBonus;

        // 최종 명중률
        float finalAccuracy = baseAccuracy 
            * movingModifier 
            * distanceModifier 
            * suppressionModifier 
            * skillModifier;

        // 0~1 범위로 클램프
        return Math.Max(0, Math.Min(1.0f, finalAccuracy));
    }

    float GetObservationAccuracy(float observationTime) {
        return observationTime switch {
            < 1f => 0.10f,   // 패닉: 10%
            < 3f => 0.25f,   // 적 파악: 25%
            < 5f => 0.50f,   // 조준: 50%
            _ => 0.75f       // 완전 집중: 75%
        };
    }

    float GetDistanceModifier(float distance) {
        // 거리에 따른 명중률 감소
        // 0m: 100%, 50m: 80%, 100m: 60%
        if (distance < 20) return 1.0f;
        if (distance < 50) return 0.9f;
        if (distance < 100) return 0.7f;
        return 0.5f;
    }

    float GetSuppressionModifier(int suppressionPercent) {
        // 제압 수준별 명중률 감소
        // 0%: 100%, 30%: 90%, 60%: 80%, 90%: 70%
        return 1.0f - (suppressionPercent * 0.001f);
    }
}
```

### 3.2 피격 반응

```csharp
public class DamageReactionSystem {
    public void OnBotDamaged(BotAgent bot, int attackerID, float damage) {
        // 피격 반응 딜레이 (인간적인 반응 시간)
        float reactionDelay = Random.Range(0.2f, 0.5f);

        // 공격자 방향 회전
        Vector3 attackerPos = GetBotPosition(attackerID);
        Vector3 directionToAttacker = (attackerPos - bot.Position).normalized;
        bot.TargetRotation = Quaternion.LookRotation(directionToAttacker);

        // 상태 전환
        bot.SetTarget(attackerID);
        bot.TransitionToState(BotState.Combat);

        // 명중률 초기화 (새로운 목표)
        bot.TargetObservationTime = 0;

        // 제압 증가
        bot.Suppression = Math.Min(100, bot.Suppression + 20);

        Console.WriteLine($"Bot {bot.BotID} 피격: 공격자 {attackerID}, 제압 +20");
    }
}
```

### 3.3 헤드샷 감지

```csharp
public class HitscanSystem {
    public struct HitResult {
        public int TargetID;
        public Vector3 HitLocation;
        public bool IsHeadshot;
        public float Damage;
    }

    public HitResult CalculateHit(
        Vector3 shootPos,
        Vector3 shootDir,
        float accuracy
    ) {
        // 정확도 기반 산탄 퍼짐 (Spread)
        Vector3 adjustedDir = ApplyInaccuracy(shootDir, 1.0f - accuracy);

        // Raycast
        RaycastHit hit;
        if (Physics.Raycast(shootPos, adjustedDir, out hit, 1000f)) {
            var target = hit.collider.GetComponent<IHittable>();
            if (target != null) {
                // 헤드샷 판정
                bool isHeadshot = IsHeadshotZone(hit.point, target);
                float damage = isHeadshot ? 50 : 25;

                return new HitResult {
                    TargetID = target.GetID(),
                    HitLocation = hit.point,
                    IsHeadshot = isHeadshot,
                    Damage = damage
                };
            }
        }

        return new HitResult(); // Miss
    }

    bool IsHeadshotZone(Vector3 hitPoint, IHittable target) {
        // 머리 높이: 170cm ~180cm 기준 (대략 170 유닛)
        // 히트 포인트가 머리 영역인지 확인
        Vector3 targetPos = target.GetPosition();
        float headHeight = targetPos.Y + 1.7f;
        
        return hitPoint.Y > headHeight - 0.2f && 
               hitPoint.Y < headHeight + 0.2f;
    }

    Vector3 ApplyInaccuracy(Vector3 direction, float inaccuracy) {
        // 정확도 기반 산탄 퍼짐
        // inaccuracy = 0: 완벽히 정조준
        // inaccuracy = 1: 최대 퍼짐 (무작위)

        float spreadAngle = inaccuracy * 30; // 최대 30도 퍼짐

        // 무작위 각도 생성
        Vector3 randomAxis = Random.onUnitSphere;
        Quaternion randomRotation = Quaternion.AngleAxis(
            Random.Range(0, spreadAngle),
            randomAxis
        );

        return randomRotation * direction;
    }
}
```

---

## 4. 제압 시스템 (Suppression)

### 4.1 제압 메커니즘

```csharp
public class SuppressionSystem {
    public class SuppressionLevel {
        public const int LIGHT = 30;      // 경미: -10% 명중률
        public const int MODERATE = 60;   // 중간: -20% 명중률, -30% 이동속도
        public const int HEAVY = 90;      // 심각: -30% 명중률, -50% 이동속도
    }

    public void UpdateSuppression(BotAgent bot, float deltaTime) {
        // 집중 사격으로부터 피해를 받고 있는가?
        bool underHeavyFire = IsUnderHeavyFire(bot);

        if (underHeavyFire) {
            // 제압 증가
            bot.Suppression = Math.Min(100, bot.Suppression + 30 * deltaTime);
        } else {
            // 제압 자동 감소
            float suppressionReduction = 5.0f * deltaTime; // 초당 5%

            if (bot.InCover) {
                // 엄폐 중: 더 빠르게 감소
                suppressionReduction *= 2.0f;
            }

            bot.Suppression = Math.Max(0, bot.Suppression - suppressionReduction);
        }

        // 제압 레벨별 효과 적용
        ApplySuppressionEffects(bot);
    }

    void ApplySuppressionEffects(BotAgent bot) {
        int suppression = (int)bot.Suppression;

        if (suppression >= SuppressionLevel.HEAVY) {
            // 심각: 강제 엄폐
            bot.MoveSpeed = bot.BaseSpeed * 0.5f;
            bot.AccuracyModifier = 0.7f;
            bot.ForceCover = true;
        } else if (suppression >= SuppressionLevel.MODERATE) {
            // 중간
            bot.MoveSpeed = bot.BaseSpeed * 0.7f;
            bot.AccuracyModifier = 0.8f;
        } else if (suppression >= SuppressionLevel.LIGHT) {
            // 경미
            bot.MoveSpeed = bot.BaseSpeed * 0.8f;
            bot.AccuracyModifier = 0.9f;
        } else {
            // 없음
            bot.MoveSpeed = bot.BaseSpeed;
            bot.AccuracyModifier = 1.0f;
        }
    }

    bool IsUnderHeavyFire(BotAgent bot) {
        // 최근 0.5초 내에 3회 이상 피격
        int recentHits = bot.DamageHistory
            .Where(d => d.Timestamp > (CurrentTime - 0.5f))
            .Count();

        return recentHits >= 3;
    }
}
```

### 4.2 제압 효과 표시

```csharp
public void VisualizeSuppressionEffect() {
    int suppression = (int)Bot.Suppression;

    // UI 표시
    SuppressionBar.SetValue(suppression);

    // 캐릭터 떨림 애니메이션
    float tremor = suppression / 100.0f;
    character.ApplyAimTremor(tremor);

    // 사운드 효과
    if (suppression > 60) {
        AudioManager.PlaySuppressionSound(suppression);
    }
}
```

---

## 5. 개별 Bot 특성

### 5.1 Bot 프로필

```csharp
public class BotProfile {
    public int BotID { get; set; }
    
    // 성격 특성 (0~1 범위 또는 0~150%)
    public float Aggression { get; set; }       // 50~150% (기본 100%)
    public float Accuracy { get; set; }         // 0~50% 보너스
    public float Courage { get; set; }          // 30~100%
    public float ReactionTime { get; set; }     // 기본값 대비 배수
    public float Discipline { get; set; }       // 엄폐 선호도

    // 특성 기반 동작 수정
    public void ApplyProfileModifiers(BotAgent bot) {
        // 공격성: 저지르는 동작 빈도 조정
        bot.FlankAttemptInterval = 5.0f / (Aggression / 100.0f);

        // 정확도: 기본 명중률 보너스
        bot.AccuracyBonus = Accuracy;

        // 용감성: 후퇴 임계값 조정
        bot.RetreatHealthThreshold = 30 * (Courage / 100.0f);

        // 반응 시간: 피격 후 응사 시간
        bot.DamageReactionDelay *= ReactionTime;

        // 규율: 엄폐 중 회복 시간
        bot.HealingDuration = 60.0f * (Discipline / 100.0f);
    }
}

public class BotProfileGenerator {
    public BotProfile GenerateRandomProfile() {
        var profile = new BotProfile {
            BotID = nextBotID++,
            Aggression = Random.Range(50, 150),
            Accuracy = Random.Range(0, 50) / 100.0f,
            Courage = Random.Range(30, 100),
            ReactionTime = Random.Range(0.7f, 1.5f),
            Discipline = Random.Range(50, 100)
        };

        return profile;
    }
}
```

### 5.2 Bot 다양성 예시

```
Bot 1: 공격적 저격수
• Aggression: 140%
• Accuracy: 40% 보너스
• Courage: 80%
→ 빠르게 추격하고 정확하게 사격

Bot 2: 신중한 보병
• Aggression: 60%
• Accuracy: 10% 보너스
• Courage: 50%
→ 엄폐 선호, 느리게 행동, 회복 중시

Bot 3: 용감한 돌격수
• Aggression: 150%
• Accuracy: 5% 보너스
• Courage: 100%
→ 적극적 측면 공격, 건강 무시
```

---

## 6. 경로 찾기 (Pathfinding)

### 6.1 A* 알고리즘

```csharp
public class Pathfinder {
    public List<Vector3> FindPath(Vector3 start, Vector3 goal) {
        var openSet = new PriorityQueue<Node>();
        var cameFrom = new Dictionary<Vector3, Vector3>();
        var gScore = new Dictionary<Vector3, float>();
        var fScore = new Dictionary<Vector3, float>();

        var startNode = new Node(start);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Enqueue(startNode, fScore[start]);

        while (openSet.Count > 0) {
            var current = openSet.Dequeue();

            if (current.Position == goal) {
                return ReconstructPath(cameFrom, goal);
            }

            foreach (var neighbor in GetNeighbors(current.Position)) {
                if (!IsWalkable(neighbor)) continue;

                float tentativeGScore = gScore[current.Position] + 
                    Vector3.Distance(current.Position, neighbor);

                if (!gScore.ContainsKey(neighbor) || 
                    tentativeGScore < gScore[neighbor]) {
                    cameFrom[neighbor] = current.Position;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + 
                        Heuristic(neighbor, goal);
                    openSet.Enqueue(
                        new Node(neighbor),
                        fScore[neighbor]
                    );
                }
            }
        }

        return new List<Vector3>(); // Path not found
    }

    float Heuristic(Vector3 a, Vector3 b) {
        // 맨해튼 거리
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + 
               Math.Abs(a.Z - b.Z);
    }
}
```

---

## 7. 커버 시스템

### 7.1 엄폐물 탐색

```csharp
public class CoverSystem {
    public Vector3 FindBestCover(Vector3 searchCenter, float searchRadius) {
        var coverPoints = new List<Vector3>();

        // 범위 내 엄폐물 찾기
        var colliders = Physics.OverlapSphere(searchCenter, searchRadius);
        foreach (var collider in colliders) {
            if (collider.CompareTag("Cover")) {
                coverPoints.Add(collider.bounds.center);
            }
        }

        if (coverPoints.Count == 0) return searchCenter;

        // 최적 엄폐물 선택
        // 기준: 거리 + 방어 가치
        Vector3 bestCover = coverPoints[0];
        float bestScore = float.MinValue;

        foreach (var cover in coverPoints) {
            float distance = Vector3.Distance(searchCenter, cover);
            float coverValue = GetCoverValue(cover);
            float score = coverValue / (distance + 1);

            if (score > bestScore) {
                bestScore = score;
                bestCover = cover;
            }
        }

        return bestCover;
    }

    float GetCoverValue(Vector3 coverPos) {
        // 엄폐물 품질 점수
        // 크기, 견고성 등으로 점수 매김
        return Random.Range(50, 100); // 임시
    }
}
```

---

## 8. 성능 최적화

### 8.1 거리 계산 최적화

```csharp
public class OptimizedDistance {
    // 제곱근 계산 피하기
    public bool IsWithinDistanceSq(Vector3 a, Vector3 b, float distanceSq) {
        return Vector3.DistanceSq(a, b) <= distanceSq;
    }

    // 사용 예
    float sightRangeMeters = 50;
    float sightRangeSq = sightRangeMeters * sightRangeMeters;

    if (IsWithinDistanceSq(bot.Position, enemy.Position, sightRangeSq)) {
        // 시야 범위 내
    }
}
```

### 8.2 객체 풀링

```csharp
public class BotObjectPool {
    private Queue<PathRequest> pathRequestPool;

    public PathRequest GetPathRequest() {
        return pathRequestPool.Count > 0 
            ? pathRequestPool.Dequeue() 
            : new PathRequest();
    }

    public void ReturnPathRequest(PathRequest request) {
        request.Reset();
        pathRequestPool.Enqueue(request);
    }
}
```

---

## 9. 모니터링 및 디버깅

### 9.1 Bot AI 통계

```csharp
public class BotAIMetrics {
    public int BotID { get; set; }
    public BotState CurrentState { get; set; }
    public float Accuracy { get; set; }
    public int KillCount { get; set; }
    public int DeathCount { get; set; }
    public float AverageAccuracy { get; set; }

    public void LogMetrics() {
        Console.WriteLine(
            $"Bot {BotID}: State={CurrentState}, " +
            $"Accuracy={Accuracy:P0}, Kills={KillCount}, " +
            $"Deaths={DeathCount}"
        );
    }
}
```

### 9.2 시각적 디버그

```
언리얼 엔진 디버그 그리기:
• 유닛 시야: 원형 선 (반경 50m)
• 이동 경로: 선 (노란색)
• 목표: 화살표 (빨강)
• 엄폐물: 박스 (녹색)
• 피격 표시: X (파강색, 1초 표시)
```

