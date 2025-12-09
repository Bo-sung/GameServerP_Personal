# RTS 게임 로직 (RTS Game Logic)

## 1. 게임 루프 아키텍처

### 1.1 메인 게임 루프
```csharp
// 게임 서버 메인 루프 (100ms 틱)
private void GameLoop()
{
    const int TickRate = 100; // 100ms per tick = 10 ticks per second
    var tickDuration = TimeSpan.FromMilliseconds(TickRate);
    
    while (gameRunning)
    {
        var tickStartTime = DateTime.UtcNow;
        
        // 1. 플레이어 입력 수집
        CollectPlayerCommands();
        
        // 2. 명령 유효성 검사 및 실행
        ValidateAndExecuteCommands();
        
        // 3. 게임 로직 처리
        UpdateGameLogic();
        
        // 4. 동기화 패킷 생성 및 전송
        SendSyncPacket();
        
        // 틱 타이밍 조정
        var elapsedTime = DateTime.UtcNow - tickStartTime;
        if (elapsedTime < tickDuration)
        {
            Thread.Sleep(tickDuration - elapsedTime);
        }
    }
}

private void UpdateGameLogic()
{
    // 1. 자원 업데이트
    UpdateResources();
    
    // 2. 건설 진행도 업데이트
    UpdateConstruction();
    
    // 3. 유닛 업데이트
    UpdateUnits();
    
    // 4. 건물 업데이트
    UpdateBuildings();
    
    // 5. 게임 상태 확인
    CheckWinConditions();
    
    // 6. 죽은 객체 정리
    CleanupDeadObjects();
}
```

### 1.2 틱 레이트와 네트워크
```
틱 레이트: 10 ticks/second (100ms)
네트워크 전송: 각 틱마다 (또는 변경 발생시)
명령 처리: 다음 틱에 적용
지연 보정: 클라이언트 로컬 예측 + 서버 보정
```

---

## 2. 자원 시스템 로직

### 2.1 자원 채취 시뮬레이션
```csharp
public class ResourceGathering
{
    private const float MINERAL_PER_WORKER_PER_SECOND = 0.5f;
    private const float GAS_PER_WORKER_PER_SECOND = 0.3f;
    private const float FOOD_PER_FARM_PER_SECOND = 0.2f;
    
    public void UpdateGathering(float deltaTime)
    {
        foreach (var player in players)
        {
            // 광물 채취
            int mineralsWorking = CountWorkersGathering(player, ResourceType.Mineral);
            float mineralGain = mineralsWorking * MINERAL_PER_WORKER_PER_SECOND * deltaTime;
            player.AddResources(ResourceType.Mineral, mineralGain);
            
            // 가스 채취
            int gasWorkers = CountWorkersGathering(player, ResourceType.Gas);
            float gasGain = gasWorkers * GAS_PER_WORKER_PER_SECOND * deltaTime;
            player.AddResources(ResourceType.Gas, gasGain);
            
            // 식량 생산
            int farmCount = CountBuildings(player, BuildingType.Farm);
            float foodGain = farmCount * FOOD_PER_FARM_PER_SECOND * deltaTime;
            player.AddResources(ResourceType.Food, foodGain);
        }
    }
    
    private int CountWorkersGathering(Player player, ResourceType type)
    {
        // 현재 자원 채취 중인 일꾼 수 계산
        return player.Units
            .OfType<Worker>()
            .Count(w => w.CurrentTask == UnitTask.Gathering && w.GatheringType == type);
    }
}
```

### 2.2 자원 제약 및 최대값
```csharp
public class PlayerResources
{
    private const int MAX_MINERALS = 5000;
    private const int MAX_GAS = 2000;
    private const int MAX_FOOD = 500;
    
    public void AddResources(ResourceType type, float amount)
    {
        amount = Math.Max(0, amount); // 음수 방지
        
        switch (type)
        {
            case ResourceType.Mineral:
                minerals = Math.Min(minerals + amount, MAX_MINERALS);
                break;
            case ResourceType.Gas:
                gas = Math.Min(gas + amount, MAX_GAS);
                break;
            case ResourceType.Food:
                food = Math.Min(food + amount, MAX_FOOD);
                break;
        }
    }
    
    public bool CanAfford(Cost cost)
    {
        return minerals >= cost.Minerals && 
               gas >= cost.Gas && 
               food >= cost.Food;
    }
    
    public void Spend(Cost cost)
    {
        if (!CanAfford(cost)) throw new InvalidOperationException("자원 부족");
        
        minerals -= cost.Minerals;
        gas -= cost.Gas;
        food -= cost.Food;
    }
    
    public void Refund(Cost cost, float percentage = 0.5f)
    {
        AddResources(ResourceType.Mineral, cost.Minerals * percentage);
        AddResources(ResourceType.Gas, cost.Gas * percentage);
        AddResources(ResourceType.Food, cost.Food * percentage);
    }
}
```

### 2.3 인구 관리 (Food System)
```csharp
public class FoodSystem
{
    public int GetMaxPopulation(Player player)
    {
        int baseFood = 15; // 초기 식량
        int farmCount = player.Buildings
            .OfType<Farm>()
            .Count();
        int farmedFood = farmCount * 10;
        
        return baseFood + farmedFood;
    }
    
    public int GetCurrentPopulation(Player player)
    {
        return player.Units.Sum(u => u.PopulationCost);
    }
    
    public bool CanTrainUnit(Player player, UnitType unitType)
    {
        var unitCost = GetUnitCost(unitType);
        var currentPopulation = GetCurrentPopulation(player);
        var maxPopulation = GetMaxPopulation(player);
        
        return currentPopulation + unitCost.PopulationCost <= maxPopulation;
    }
}
```

---

## 3. 유닛 시스템 로직

### 3.1 유닛 생산
```csharp
public class UnitProduction
{
    public class ProductionQueue
    {
        public List<QueuedUnit> queue = new();
        public float productionProgress = 0; // 0~1
        public Unit currentUnit = null;
        
        public void QueueUnit(UnitType type, Player owner)
        {
            queue.Add(new QueuedUnit { Type = type, Owner = owner });
        }
        
        public void Update(float deltaTime)
        {
            if (queue.Count == 0) return;
            
            var unitType = queue[0].Type;
            var productionTime = GetProductionTime(unitType);
            
            productionProgress += deltaTime / productionTime;
            
            if (productionProgress >= 1.0f)
            {
                // 유닛 생성 완료
                var newUnit = CreateUnit(unitType);
                queue[0].Owner.Units.Add(newUnit);
                
                queue.RemoveAt(0);
                productionProgress = 0;
            }
        }
    }
    
    // 생산 시간 (초 단위)
    private static float GetProductionTime(UnitType type)
    {
        return type switch
        {
            UnitType.Worker => 4f,
            UnitType.Infantry => 6f,
            UnitType.Archer => 8f,
            UnitType.Cavalry => 10f,
            UnitType.Wizard => 12f,
            _ => 0f
        };
    }
}
```

### 3.2 유닛 이동 및 경로 찾기
```csharp
public class UnitMovement
{
    public class Unit
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Speed { get; set; } // 타일/초
        public List<Vector2> PathQueue { get; set; } = new();
        
        public void Update(float deltaTime, Pathfinder pathfinder)
        {
            if (PathQueue.Count == 0) return;
            
            var targetPos = PathQueue[0];
            var direction = (targetPos - Position).Normalized();
            Position += direction * Speed * deltaTime;
            
            // 목표 도달 확인
            if (Vector2.Distance(Position, targetPos) < 0.1f)
            {
                PathQueue.RemoveAt(0);
            }
        }
        
        public void MoveTo(Vector2 destination, Pathfinder pathfinder)
        {
            PathQueue = pathfinder.FindPath(Position, destination);
        }
    }
}

// A* 알고리즘을 사용한 경로 찾기
public class Pathfinder
{
    private Map map;
    
    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        var openSet = new PriorityQueue<Node>();
        var cameFrom = new Dictionary<Vector2, Vector2>();
        var gScore = new Dictionary<Vector2, float>();
        var fScore = new Dictionary<Vector2, float>();
        
        var startNode = new Node(start, 0, Heuristic(start, goal));
        openSet.Enqueue(startNode, startNode.FScore);
        
        // A* 알고리즘 구현
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            
            if (current.Position == goal)
            {
                return ReconstructPath(cameFrom, goal);
            }
            
            foreach (var neighbor in GetNeighbors(current.Position))
            {
                if (!map.IsWalkable(neighbor)) continue;
                
                float tentativeGScore = gScore[current.Position] + 1;
                
                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current.Position;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);
                    
                    openSet.Enqueue(new Node(neighbor, tentativeGScore, fScore[neighbor]),
                                   fScore[neighbor]);
                }
            }
        }
        
        return new List<Vector2>(); // 경로 없음
    }
    
    private float Heuristic(Vector2 a, Vector2 b)
    {
        // Manhattan Distance
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
```

### 3.3 유닛 전투 시스템
```csharp
public class Combat
{
    public class AttackCommand
    {
        public Unit Attacker { get; set; }
        public Unit Target { get; set; }
        public float AttackCooldown { get; set; }
        public float RemainingCooldown { get; set; }
    }
    
    public void ResolveAttack(Unit attacker, Unit target)
    {
        if (attacker.RemainingCooldown > 0) return; // 쿨다운 중
        
        // 데미지 계산
        float baseDamage = attacker.AttackPower;
        float armorReduction = target.Armor * 0.1f;
        float actualDamage = Math.Max(baseDamage - armorReduction, 1f);
        
        // 크리티컬 계산 (20% 확률)
        if (Random.Value < 0.2f)
        {
            actualDamage *= 1.5f; // 1.5배 크리티컬
        }
        
        // 데미지 적용
        target.Health -= actualDamage;
        
        // 공격 쿨다운 설정
        attacker.RemainingCooldown = attacker.AttackCooldown;
        
        // 죽음 처리
        if (target.Health <= 0)
        {
            target.Die();
        }
    }
    
    public void UpdateCombat(float deltaTime)
    {
        foreach (var unit in allUnits)
        {
            if (unit.RemainingCooldown > 0)
            {
                unit.RemainingCooldown -= deltaTime;
            }
            
            if (unit.CurrentTarget != null && unit.CurrentTarget.IsAlive)
            {
                // 범위 내인지 확인
                if (IsInAttackRange(unit, unit.CurrentTarget))
                {
                    ResolveAttack(unit, unit.CurrentTarget);
                }
                else
                {
                    // 목표까지 이동
                    unit.MoveTo(unit.CurrentTarget.Position, pathfinder);
                }
            }
        }
    }
    
    private bool IsInAttackRange(Unit attacker, Unit target)
    {
        float distance = Vector2.Distance(attacker.Position, target.Position);
        return distance <= attacker.AttackRange;
    }
}
```

---

## 4. 건물 시스템 로직

### 4.1 건설 시스템
```csharp
public class Construction
{
    public class ConstructionSite
    {
        public BuildingType Type { get; set; }
        public Vector2 Position { get; set; }
        public Player Owner { get; set; }
        public float ConstructionProgress { get; set; } // 0~1
        public Unit BuildingWorker { get; set; }
        public float ConstructionTime { get; set; }
        
        public void Update(float deltaTime)
        {
            if (BuildingWorker == null || !BuildingWorker.IsAlive)
            {
                return; // 일꾼이 없으면 정지
            }
            
            ConstructionProgress += deltaTime / ConstructionTime;
            
            if (ConstructionProgress >= 1.0f)
            {
                Complete();
            }
        }
        
        private void Complete()
        {
            var building = CreateBuilding(Type, Owner, Position);
            Owner.Buildings.Add(building);
            // 건설 완료 이벤트
        }
    }
    
    public static bool CanBuildAt(Vector2 position, BuildingType type, Map map)
    {
        // 1. 지형 검사
        if (!map.IsValidBuildTerrain(position)) return false;
        
        // 2. 겹침 검사
        if (map.HasBuildingAt(position)) return false;
        
        // 3. 건물 인접 검사 (경로 연결성)
        if (!IsAdjacentToFriendlyBuilding(position)) return false;
        
        return true;
    }
    
    public void StartConstruction(Vector2 position, BuildingType type, 
                                  Player player, Unit worker)
    {
        if (!CanBuildAt(position, type, map))
            throw new InvalidOperationException("건설 불가 위치");
        
        if (!player.Resources.CanAfford(GetBuildingCost(type)))
            throw new InvalidOperationException("자원 부족");
        
        player.Resources.Spend(GetBuildingCost(type));
        
        var site = new ConstructionSite
        {
            Type = type,
            Position = position,
            Owner = player,
            BuildingWorker = worker,
            ConstructionTime = GetConstructionTime(type)
        };
        
        ConstructionSites.Add(site);
    }
    
    public void CancelConstruction(ConstructionSite site)
    {
        var refundCost = GetBuildingCost(site.Type);
        site.Owner.Resources.Refund(refundCost, 0.5f); // 50% 환급
        ConstructionSites.Remove(site);
    }
}
```

### 4.2 건물 업그레이드
```csharp
public class BuildingUpgrade
{
    public enum UpgradeType
    {
        Weapon,      // 공격력 +1
        Armor,       // 방어력 +10%
        ProductionSpeed, // 생산 속도 -5%
        GatherSpeed  // 채취 속도 +10%
    }
    
    public class Upgrade
    {
        public UpgradeType Type { get; set; }
        public int Level { get; set; }
        public float ResearchProgress { get; set; }
        public float ResearchTime { get; set; }
        public Cost Cost { get; set; }
        public Building RequiredBuilding { get; set; }
    }
    
    public void StartResearch(Building researchFacility, UpgradeType type)
    {
        var upgrade = GetUpgrade(type);
        
        if (!researchFacility.Owner.Resources.CanAfford(upgrade.Cost))
            throw new InvalidOperationException("자원 부족");
        
        researchFacility.Owner.Resources.Spend(upgrade.Cost);
        researchFacility.CurrentResearch = upgrade;
    }
    
    public void UpdateResearch(Building researchFacility, float deltaTime)
    {
        if (researchFacility.CurrentResearch == null) return;
        
        var upgrade = researchFacility.CurrentResearch;
        upgrade.ResearchProgress += deltaTime / upgrade.ResearchTime;
        
        if (upgrade.ResearchProgress >= 1.0f)
        {
            CompleteResearch(upgrade);
            researchFacility.CurrentResearch = null;
        }
    }
    
    private void CompleteResearch(Upgrade upgrade)
    {
        upgrade.Level++;
        
        // 업그레이드 효과 적용
        switch (upgrade.Type)
        {
            case UpgradeType.Weapon:
                ApplyWeaponUpgrade(upgrade);
                break;
            case UpgradeType.Armor:
                ApplyArmorUpgrade(upgrade);
                break;
            case UpgradeType.ProductionSpeed:
                ApplyProductionSpeedUpgrade(upgrade);
                break;
            case UpgradeType.GatherSpeed:
                ApplyGatherSpeedUpgrade(upgrade);
                break;
        }
    }
}
```

---

## 5. 명령 처리 시스템

### 5.1 명령 구조체
```csharp
public abstract class GameCommand
{
    public int PlayerId { get; set; }
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; } // 명령 순서 추적
    
    public abstract void Execute(GameState gameState);
    public abstract bool Validate(GameState gameState);
}

public class MoveCommand : GameCommand
{
    public int[] UnitIds { get; set; }
    public Vector2 TargetPosition { get; set; }
    
    public override void Execute(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        foreach (var unitId in UnitIds)
        {
            var unit = player.GetUnit(unitId);
            if (unit != null)
            {
                unit.MoveTo(TargetPosition, gameState.Pathfinder);
            }
        }
    }
    
    public override bool Validate(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        return player != null && 
               gameState.Map.IsWalkable(TargetPosition);
    }
}

public class AttackCommand : GameCommand
{
    public int[] AttackerUnitIds { get; set; }
    public int TargetUnitId { get; set; }
    
    public override void Execute(GameState gameState)
    {
        var targetUnit = gameState.GetUnit(TargetUnitId);
        if (targetUnit == null) return;
        
        var player = gameState.GetPlayer(PlayerId);
        foreach (var unitId in AttackerUnitIds)
        {
            var unit = player.GetUnit(unitId);
            if (unit != null && unit.CanAttack)
            {
                unit.CurrentTarget = targetUnit;
            }
        }
    }
    
    public override bool Validate(GameState gameState)
    {
        var targetUnit = gameState.GetUnit(TargetUnitId);
        return targetUnit != null && !targetUnit.Owner.Equals(gameState.GetPlayer(PlayerId));
    }
}

public class BuildCommand : GameCommand
{
    public BuildingType BuildingType { get; set; }
    public Vector2 Position { get; set; }
    public int WorkerId { get; set; }
    
    public override void Execute(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        var worker = player.GetUnit(WorkerId) as Worker;
        
        if (worker != null && Construction.CanBuildAt(Position, BuildingType, gameState.Map))
        {
            Construction.StartConstruction(Position, BuildingType, player, worker);
        }
    }
    
    public override bool Validate(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        if (!player.Resources.CanAfford(Construction.GetBuildingCost(BuildingType)))
            return false;
        
        return Construction.CanBuildAt(Position, BuildingType, gameState.Map);
    }
}

public class TrainCommand : GameCommand
{
    public BuildingType TrainingBuilding { get; set; }
    public UnitType UnitType { get; set; }
    
    public override void Execute(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        var building = player.GetBuilding(TrainingBuilding);
        
        if (building != null && player.Resources.CanAfford(UnitProduction.GetUnitCost(UnitType)))
        {
            player.Resources.Spend(UnitProduction.GetUnitCost(UnitType));
            building.ProductionQueue.QueueUnit(UnitType, player);
        }
    }
    
    public override bool Validate(GameState gameState)
    {
        var player = gameState.GetPlayer(PlayerId);
        return player.Resources.CanAfford(UnitProduction.GetUnitCost(UnitType)) &&
               FoodSystem.CanTrainUnit(player, UnitType);
    }
}
```

### 5.2 명령 큐 및 처리
```csharp
public class CommandProcessor
{
    private Queue<GameCommand> commandQueue = new();
    private Dictionary<int, int> lastSequenceNumber = new();
    
    public void QueueCommand(GameCommand command)
    {
        // 명령 검증
        if (!command.Validate(gameState))
        {
            SendErrorToClient(command.PlayerId, "명령 검증 실패");
            return;
        }
        
        // 중복 명령 확인
        if (lastSequenceNumber.TryGetValue(command.PlayerId, out int lastSeq))
        {
            if (command.SequenceNumber <= lastSeq)
            {
                return; // 이미 처리한 명령
            }
        }
        
        commandQueue.Enqueue(command);
        lastSequenceNumber[command.PlayerId] = command.SequenceNumber;
    }
    
    public void ProcessCommands()
    {
        while (commandQueue.Count > 0)
        {
            var command = commandQueue.Dequeue();
            command.Execute(gameState);
        }
    }
}
```

---

## 6. 게임 상태 동기화

### 6.1 상태 직렬화
```csharp
public class GameStateDelta
{
    public List<UnitUpdate> UnitUpdates { get; set; }
    public List<BuildingUpdate> BuildingUpdates { get; set; }
    public List<ResourceUpdate> ResourceUpdates { get; set; }
    public List<int> DeadUnitIds { get; set; }
    public long Timestamp { get; set; }
}

public class UnitUpdate
{
    public int UnitId { get; set; }
    public Vector2 Position { get; set; }
    public float Health { get; set; }
    public UnitState State { get; set; }
    public int? TargetUnitId { get; set; }
}

public class BuildingUpdate
{
    public int BuildingId { get; set; }
    public float Health { get; set; }
    public float ConstructionProgress { get; set; }
    public float ResearchProgress { get; set; }
}

public class ResourceUpdate
{
    public int PlayerId { get; set; }
    public float Minerals { get; set; }
    public float Gas { get; set; }
    public int Food { get; set; }
    public int CurrentPopulation { get; set; }
}
```

### 6.2 부분 동기화 (Delta Sync)
```csharp
public class DeltaSyncManager
{
    private GameState previousState;
    private GameState currentState;
    
    public GameStateDelta ComputeDelta()
    {
        var delta = new GameStateDelta { Timestamp = DateTime.UtcNow.Ticks };
        
        // 변경된 유닛만 전송
        foreach (var unit in currentState.AllUnits)
        {
            var prevUnit = previousState.GetUnit(unit.Id);
            
            if (prevUnit == null || HasChanged(unit, prevUnit))
            {
                delta.UnitUpdates.Add(new UnitUpdate
                {
                    UnitId = unit.Id,
                    Position = unit.Position,
                    Health = unit.Health,
                    State = unit.State,
                    TargetUnitId = unit.CurrentTarget?.Id
                });
            }
        }
        
        // 죽은 유닛 처리
        foreach (var prevUnit in previousState.AllUnits)
        {
            if (currentState.GetUnit(prevUnit.Id) == null)
            {
                delta.DeadUnitIds.Add(prevUnit.Id);
            }
        }
        
        return delta;
    }
    
    private bool HasChanged(Unit current, Unit previous)
    {
        return !current.Position.Equals(previous.Position) ||
               current.Health != previous.Health ||
               current.State != previous.State ||
               current.CurrentTarget?.Id != previous.CurrentTarget?.Id;
    }
}
```

---

## 7. 포그 오브 워 (Fog of War) 로직

### 7.1 시야 계산
```csharp
public class FogOfWar
{
    private bool[,] visibilityMap; // 현재 보이는 영역
    private bool[,] exploredMap;   // 탐험된 영역
    
    public void UpdateFogOfWar(Player player)
    {
        // 시야 초기화
        Array.Clear(visibilityMap, 0, visibilityMap.Length);
        
        // 플레이어의 모든 유닛에서 시야 범위 계산
        foreach (var unit in player.Units)
        {
            RevealVision(unit.Position, unit.VisionRange);
        }
        
        // 플레이어의 모든 건물에서 시야 범위 계산
        foreach (var building in player.Buildings)
        {
            RevealVision(building.Position, building.VisionRange);
        }
    }
    
    private void RevealVision(Vector2 center, float range)
    {
        int startX = Math.Max(0, (int)(center.X - range));
        int endX = Math.Min(visibilityMap.GetLength(0), (int)(center.X + range));
        int startY = Math.Max(0, (int)(center.Y - range));
        int endY = Math.Min(visibilityMap.GetLength(1), (int)(center.Y + range));
        
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                float distance = Vector2.Distance(center, new Vector2(x, y));
                if (distance <= range)
                {
                    visibilityMap[x, y] = true;
                    exploredMap[x, y] = true; // 탐험됨 표시
                }
            }
        }
    }
    
    public FogOfWarState GetFogState(int tileX, int tileY)
    {
        if (visibilityMap[tileX, tileY])
            return FogOfWarState.Visible;
        else if (exploredMap[tileX, tileY])
            return FogOfWarState.Explored;
        else
            return FogOfWarState.Unexplored;
    }
}

public enum FogOfWarState
{
    Unexplored,  // 본 적 없음
    Explored,    // 이전에 봤음 (그레이스케일)
    Visible      // 현재 보임 (명확함)
}
```

### 7.2 클라이언트에 포그 정보 전송
```csharp
public class FogOfWarSync
{
    public void SendFogOfWarUpdate(Player player, GameStatePacket packet)
    {
        var fogData = new FogOfWarUpdate();
        
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var state = player.FogOfWar.GetFogState(x, y);
                
                // 가시 영역만 유닛/건물 정보 전송
                if (state == FogOfWarState.Visible)
                {
                    var unit = GetUnitAt(x, y);
                    var building = GetBuildingAt(x, y);
                    
                    // 적 유닛/건물만 보임
                    if (unit != null && unit.Owner != player)
                        fogData.VisibleUnits.Add(unit.Id);
                    if (building != null && building.Owner != player)
                        fogData.VisibleBuildings.Add(building.Id);
                }
                
                fogData.FogState[x, y] = (byte)state;
            }
        }
        
        packet.FogOfWar = fogData;
    }
}
```

---

## 8. 승리 조건 확인

### 8.1 게임 승리 로직
```csharp
public class WinConditionChecker
{
    public GameResult CheckWinConditions(GameState gameState)
    {
        // 1. 기지 파괴 확인
        var baseDestroyedPlayers = gameState.Players
            .Where(p => !p.HasMainBase)
            .ToList();
        
        if (baseDestroyedPlayers.Count == gameState.Players.Count - 1)
        {
            // 기지를 가진 유일한 플레이어가 승리
            var winner = gameState.Players.First(p => p.HasMainBase);
            return new GameResult
            {
                Winner = winner,
                WinReason = WinReason.BaseDestroyed,
                Timestamp = DateTime.UtcNow
            };
        }
        
        // 2. 점수 기반 승리 (선택적)
        if (gameState.GameMode.HasScoreWin)
        {
            var leader = gameState.Players.OrderByDescending(p => p.Score).First();
            if (leader.Score >= gameState.GameMode.ScoreGoal)
            {
                return new GameResult
                {
                    Winner = leader,
                    WinReason = WinReason.ScoreGoal,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        // 3. 모든 플레이어가 항복한 경우
        var activePlayers = gameState.Players.Where(p => p.IsActive).Count();
        if (activePlayers == 1)
        {
            var winner = gameState.Players.First(p => p.IsActive);
            return new GameResult
            {
                Winner = winner,
                WinReason = WinReason.OpponentSurrender,
                Timestamp = DateTime.UtcNow
            };
        }
        
        return null; // 게임 진행 중
    }
}
```

### 8.2 항복 처리
```csharp
public class SurrenderHandler
{
    public void ProcessSurrender(Player player)
    {
        player.IsActive = false;
        
        // 플레이어의 모든 유닛/건물 제거
        player.Units.Clear();
        player.Buildings.Clear();
        
        // 연합군(팀 게임)에 알림
        NotifyAllies(player, "팀원이 항복했습니다");
        
        // 게임 결과 확인
        CheckGameEnd();
    }
}
```

---

## 9. 최적화 기법

### 9.1 공간 분할 (Quadtree)
```csharp
public class Quadtree
{
    private Node root;
    
    public class Node
    {
        public Rectangle Bounds { get; set; }
        public List<Unit> Units { get; set; }
        public Node[] Children { get; set; } // 4개의 자식 노드
        public int Depth { get; set; }
        
        public const int MAX_UNITS_PER_NODE = 10;
        public const int MAX_DEPTH = 5;
    }
    
    public void Insert(Unit unit)
    {
        InsertIntoNode(root, unit);
    }
    
    private void InsertIntoNode(Node node, Unit unit)
    {
        if (!node.Bounds.Contains(unit.Position))
            return;
        
        node.Units.Add(unit);
        
        // 노드가 너무 많은 유닛을 가지면 분할
        if (node.Units.Count > Node.MAX_UNITS_PER_NODE && 
            node.Depth < Node.MAX_DEPTH && 
            node.Children == null)
        {
            SplitNode(node);
        }
        
        // 자식 노드에 삽입
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child.Bounds.Contains(unit.Position))
                {
                    InsertIntoNode(child, unit);
                }
            }
        }
    }
    
    public List<Unit> GetNearbyUnits(Vector2 position, float radius)
    {
        var result = new List<Unit>();
        GetNearbyUnitsInNode(root, position, radius, result);
        return result;
    }
    
    private void GetNearbyUnitsInNode(Node node, Vector2 center, 
                                     float radius, List<Unit> result)
    {
        if (!node.Bounds.Intersects(new Circle(center, radius)))
            return;
        
        foreach (var unit in node.Units)
        {
            if (Vector2.Distance(unit.Position, center) <= radius)
                result.Add(unit);
        }
        
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                GetNearbyUnitsInNode(child, center, radius, result);
            }
        }
    }
}
```

### 9.2 LOD (Level of Detail)
```csharp
public class LevelOfDetail
{
    public void UpdateUnitLOD(Unit unit, Vector2 playerCameraPos)
    {
        float distance = Vector2.Distance(unit.Position, playerCameraPos);
        
        if (distance < 50) // 가까움
        {
            unit.DetailLevel = DetailLevel.High;
            unit.UpdateFrequency = 10; // 매 틱마다 업데이트
        }
        else if (distance < 150) // 중간
        {
            unit.DetailLevel = DetailLevel.Medium;
            unit.UpdateFrequency = 20; // 2틱마다 업데이트
        }
        else // 멀음
        {
            unit.DetailLevel = DetailLevel.Low;
            unit.UpdateFrequency = 50; // 5틱마다 업데이트
        }
    }
}
```

---

## 10. 네트워크 패킷 최적화

### 10.1 명령 배치 처리
```csharp
public class CommandBatcher
{
    private List<GameCommand> pendingCommands = new();
    private float batchInterval = 0.1f; // 100ms
    private float timeSinceLastBatch = 0;
    
    public void AddCommand(GameCommand command)
    {
        pendingCommands.Add(command);
    }
    
    public void Update(float deltaTime)
    {
        timeSinceLastBatch += deltaTime;
        
        if (timeSinceLastBatch >= batchInterval && pendingCommands.Count > 0)
        {
            SendCommandBatch();
            timeSinceLastBatch = 0;
        }
    }
    
    private void SendCommandBatch()
    {
        var packet = new CommandBatchPacket
        {
            Commands = pendingCommands.ToList(),
            Timestamp = DateTime.UtcNow.Ticks
        };
        
        networkManager.SendPacket(packet);
        pendingCommands.Clear();
    }
}
```

