# FPS 게임 로직

## 개요

서버에서 실행되는 FPS 게임의 핵심 로직을 정의합니다.

---

## 게임 상태 머신

### 상태 정의
```csharp
public enum GameState
{
    Warmup,      // 대기 중
    Playing,     // 게임 진행 중
    Finished     // 게임 종료
}
```

### 상태 전이
```csharp
public class GameStateMachine
{
    private GameState currentState = GameState.Warmup;
    private float stateTimer = 0;

    public void Update(float deltaTime)
    {
        stateTimer += deltaTime;

        switch (currentState)
        {
            case GameState.Warmup:
                UpdateWarmup();
                break;

            case GameState.Playing:
                UpdatePlaying();
                break;

            case GameState.Finished:
                UpdateFinished();
                break;
        }
    }

    private void UpdateWarmup()
    {
        // 최소 인원 확인 (예: 2명)
        if (GetPlayerCount() >= MIN_PLAYERS)
        {
            // 카운트다운 시작 (10초)
            if (stateTimer >= WARMUP_DURATION)
            {
                TransitionToPlaying();
            }
        }
        else
        {
            // 인원 부족 시 타이머 리셋
            stateTimer = 0;
        }
    }

    private void UpdatePlaying()
    {
        // 승리 조건 체크
        if (CheckWinCondition())
        {
            TransitionToFinished();
        }

        // 시간 종료 체크
        if (stateTimer >= GAME_DURATION)
        {
            TransitionToFinished();
        }
    }

    private void UpdateFinished()
    {
        // 10초 후 게임 종료 또는 재시작
        if (stateTimer >= 10.0f)
        {
            EndGame();
        }
    }

    private void TransitionToPlaying()
    {
        currentState = GameState.Playing;
        stateTimer = 0;

        // 모든 플레이어 스폰
        SpawnAllPlayers();

        // 게임 시작 알림
        BroadcastGameStateChange(GameState.Playing);
    }

    private void TransitionToFinished()
    {
        currentState = GameState.Finished;
        stateTimer = 0;

        // 최종 순위 계산
        CalculateFinalRankings();

        // 게임 종료 알림
        BroadcastGameStateChange(GameState.Finished);

        // 통계 저장
        SaveGameStats();
    }
}
```

---

## 플레이어 관리

### 플레이어 클래스
```csharp
public class ServerPlayer
{
    // 식별자
    public string UserId { get; set; }
    public string Username { get; set; }
    public byte Team { get; set; }  // 0=neutral, 1=red, 2=blue

    // 상태
    public bool IsAlive { get; set; }
    public int Health { get; set; }
    public int Armor { get; set; }

    // 위치 및 물리
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public bool IsGrounded { get; set; }
    public bool IsCrouching { get; set; }
    public bool IsSprinting { get; set; }

    // 무기
    public Weapon CurrentWeapon { get; set; }
    public Dictionary<string, int> Ammunition { get; set; }

    // 통계
    public PlayerGameStats Stats { get; set; }

    // 히스토리 (렉 보상용)
    public PlayerHistory History { get; set; }

    // 입력 큐
    public Queue<PlayerInput> InputQueue { get; set; }
    public uint LastProcessedInput { get; set; }

    // 리스폰
    public float RespawnTimer { get; set; }
}

public class PlayerGameStats
{
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Headshots { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int CurrentKillstreak { get; set; }
    public int HighestKillstreak { get; set; }
    public float DistanceTraveled { get; set; }
}
```

---

## 스폰 시스템

### 스폰 포인트 선택
```csharp
public class SpawnSystem
{
    private List<SpawnPoint> spawnPoints = new();

    public SpawnPoint SelectSpawnPoint(ServerPlayer player)
    {
        // 팀 기반 게임이면 팀 스폰 포인트 사용
        if (GameMode == "tdm" || GameMode == "ctf")
        {
            return SelectTeamSpawnPoint(player.Team);
        }

        // FFA는 가장 안전한 스폰 포인트 선택
        return SelectSafestSpawnPoint();
    }

    private SpawnPoint SelectTeamSpawnPoint(byte team)
    {
        var teamSpawns = spawnPoints
            .Where(sp => sp.Team == team)
            .ToList();

        if (teamSpawns.Count == 0)
            return spawnPoints[0];  // 기본 스폰

        // 랜덤 선택
        return teamSpawns[Random.Range(0, teamSpawns.Count)];
    }

    private SpawnPoint SelectSafestSpawnPoint()
    {
        SpawnPoint safest = null;
        float maxDistance = 0;

        foreach (var spawnPoint in spawnPoints)
        {
            // 가장 가까운 적과의 거리 계산
            float minDistanceToEnemy = float.MaxValue;

            foreach (var player in GameServer.AllPlayers)
            {
                if (!player.IsAlive)
                    continue;

                float distance = Vector3.Distance(spawnPoint.Position, player.Position);

                if (distance < minDistanceToEnemy)
                {
                    minDistanceToEnemy = distance;
                }
            }

            // 가장 먼 스폰 포인트 선택
            if (minDistanceToEnemy > maxDistance)
            {
                maxDistance = minDistanceToEnemy;
                safest = spawnPoint;
            }
        }

        return safest ?? spawnPoints[0];
    }

    public void SpawnPlayer(ServerPlayer player)
    {
        var spawnPoint = SelectSpawnPoint(player);

        player.Position = spawnPoint.Position;
        player.Rotation = spawnPoint.Rotation;
        player.Health = 100;
        player.Armor = 0;
        player.IsAlive = true;
        player.Velocity = Vector3.Zero;

        // 기본 무기 지급
        GiveDefaultWeapon(player);

        // 스폰 알림
        BroadcastPlayerSpawn(player);
    }

    private void GiveDefaultWeapon(ServerPlayer player)
    {
        player.CurrentWeapon = WeaponDatabase.Get("ar_default");
        player.Ammunition["ar_default"] = 90;  // 예비 탄약
    }
}
```

---

## 무기 시스템

### 무기 데이터
```csharp
public class Weapon
{
    public string Id { get; set; }
    public string Name { get; set; }
    public WeaponType Type { get; set; }

    // 데미지
    public int Damage { get; set; }
    public float Range { get; set; }

    // 발사 속도
    public int FireRate { get; set; }  // RPM
    public float FireInterval => 60f / FireRate;

    // 탄창
    public int MagazineSize { get; set; }
    public float ReloadTime { get; set; }

    // 반동
    public Vector2 Recoil { get; set; }  // (수직, 수평)

    // 정확도
    public float Accuracy { get; set; }

    // 특수 (샷건용)
    public int Pellets { get; set; }
    public float Spread { get; set; }

    // 특수 (투사체용)
    public float ProjectileSpeed { get; set; }
    public float SplashRadius { get; set; }
    public int SplashDamage { get; set; }

    // 상태
    public int CurrentAmmo { get; set; }
    public float LastFireTime { get; set; }
    public bool IsReloading { get; set; }
    public float ReloadStartTime { get; set; }
}

public enum WeaponType
{
    Hitscan,
    HitscanSpread,  // 샷건
    Projectile
}
```

### 발사 처리
```csharp
public class WeaponSystem
{
    public void HandleWeaponFire(ServerPlayer shooter, WeaponFirePacket packet)
    {
        var weapon = shooter.CurrentWeapon;

        // 1. 발사 가능 검증
        if (!CanFire(shooter, weapon))
            return;

        // 2. 발사 처리
        weapon.LastFireTime = GameServer.CurrentTime;
        weapon.CurrentAmmo--;

        // 3. 통계 업데이트
        shooter.Stats.ShotsFired++;

        // 4. 무기 타입별 처리
        switch (weapon.Type)
        {
            case WeaponType.Hitscan:
                HandleHitscanFire(shooter, weapon, packet);
                break;

            case WeaponType.HitscanSpread:
                HandleShotgunFire(shooter, weapon, packet);
                break;

            case WeaponType.Projectile:
                HandleProjectileFire(shooter, weapon, packet);
                break;
        }
    }

    private bool CanFire(ServerPlayer shooter, Weapon weapon)
    {
        // 생존 확인
        if (!shooter.IsAlive)
            return false;

        // 재장전 중 확인
        if (weapon.IsReloading)
            return false;

        // 탄약 확인
        if (weapon.CurrentAmmo <= 0)
        {
            // 자동 재장전
            StartReload(shooter, weapon);
            return false;
        }

        // 발사 속도 확인
        float timeSinceLastFire = GameServer.CurrentTime - weapon.LastFireTime;
        if (timeSinceLastFire < weapon.FireInterval)
            return false;

        return true;
    }

    private void HandleHitscanFire(ServerPlayer shooter, Weapon weapon, WeaponFirePacket packet)
    {
        // 히트 판정
        var hitResult = HitDetection.PerformHitscan(
            shooter,
            packet.FirePosition,
            packet.FireDirection,
            weapon.Range,
            packet.Timestamp
        );

        if (hitResult != null)
        {
            // 적중
            shooter.Stats.ShotsHit++;

            // 데미지 적용
            int damage = (int)(weapon.Damage * hitResult.DamageMultiplier);
            DamageSystem.ApplyDamage(
                hitResult.HitPlayer,
                shooter,
                damage,
                hitResult.DamageMultiplier,
                hitResult.HitboxName
            );

            shooter.Stats.DamageDealt += damage;

            // 히트 정보와 함께 브로드캐스트
            BroadcastWeaponFire(shooter, weapon, hitResult);
        }
        else
        {
            // 빗나감
            BroadcastWeaponFire(shooter, weapon, null);
        }
    }

    private void HandleShotgunFire(ServerPlayer shooter, Weapon weapon, WeaponFirePacket packet)
    {
        var hits = HitDetection.PerformShotgunBlast(
            shooter,
            packet.FirePosition,
            packet.FireDirection,
            weapon.Pellets,
            weapon.Spread,
            weapon.Range,
            packet.Timestamp,
            packet.SpreadSeed
        );

        shooter.Stats.ShotsHit += hits.Count;

        foreach (var hit in hits)
        {
            int damage = (int)(weapon.Damage * hit.DamageMultiplier);
            DamageSystem.ApplyDamage(
                hit.HitPlayer,
                shooter,
                damage,
                hit.DamageMultiplier,
                hit.HitboxName
            );

            shooter.Stats.DamageDealt += damage;
        }

        BroadcastWeaponFire(shooter, weapon, hits);
    }

    private void HandleProjectileFire(ServerPlayer shooter, Weapon weapon, WeaponFirePacket packet)
    {
        // 투사체 생성
        var projectile = new Projectile
        {
            Id = GenerateProjectileId(),
            Owner = shooter,
            Position = packet.FirePosition,
            Velocity = packet.FireDirection * weapon.ProjectileSpeed,
            Damage = weapon.Damage,
            SplashRadius = weapon.SplashRadius,
            SplashDamage = weapon.SplashDamage
        };

        GameServer.SpawnProjectile(projectile);

        // 브로드캐스트
        BroadcastProjectileSpawn(projectile);
    }

    public void HandleReload(ServerPlayer player)
    {
        var weapon = player.CurrentWeapon;

        // 재장전 가능 확인
        if (weapon.IsReloading)
            return;

        if (weapon.CurrentAmmo >= weapon.MagazineSize)
            return;

        if (!player.Ammunition.TryGetValue(weapon.Id, out int reserveAmmo) || reserveAmmo <= 0)
            return;

        // 재장전 시작
        StartReload(player, weapon);
    }

    private void StartReload(ServerPlayer player, Weapon weapon)
    {
        weapon.IsReloading = true;
        weapon.ReloadStartTime = GameServer.CurrentTime;

        // 재장전 타이머 설정
        GameServer.ScheduleCallback(weapon.ReloadTime, () =>
        {
            FinishReload(player, weapon);
        });

        // 브로드캐스트
        BroadcastPlayerStateChange(player);
    }

    private void FinishReload(ServerPlayer player, Weapon weapon)
    {
        if (!player.Ammunition.TryGetValue(weapon.Id, out int reserveAmmo))
            return;

        // 필요한 탄약 계산
        int needed = weapon.MagazineSize - weapon.CurrentAmmo;
        int reload = Math.Min(needed, reserveAmmo);

        // 탄약 이동
        weapon.CurrentAmmo += reload;
        player.Ammunition[weapon.Id] -= reload;

        weapon.IsReloading = false;

        // 브로드캐스트
        BroadcastPlayerStateChange(player);
    }
}
```

---

## 아이템 시스템

### 아이템 스폰
```csharp
public class ItemSystem
{
    private List<ItemSpawn> itemSpawns = new();

    public void Update(float deltaTime)
    {
        foreach (var itemSpawn in itemSpawns)
        {
            if (!itemSpawn.IsActive && itemSpawn.RespawnTimer <= 0)
            {
                // 리스폰
                SpawnItem(itemSpawn);
            }
            else if (!itemSpawn.IsActive)
            {
                itemSpawn.RespawnTimer -= deltaTime;
            }
        }
    }

    private void SpawnItem(ItemSpawn itemSpawn)
    {
        itemSpawn.IsActive = true;

        // 아이템 스폰 알림
        BroadcastItemSpawn(itemSpawn);
    }

    public void HandleItemPickup(ServerPlayer player, string itemId)
    {
        var itemSpawn = itemSpawns.FirstOrDefault(i => i.Id == itemId);

        if (itemSpawn == null || !itemSpawn.IsActive)
            return;

        // 아이템 효과 적용
        ApplyItemEffect(player, itemSpawn.Type);

        // 아이템 비활성화
        itemSpawn.IsActive = false;
        itemSpawn.RespawnTimer = itemSpawn.RespawnTime;

        // 브로드캐스트
        BroadcastItemPickup(player, itemSpawn);
    }

    private void ApplyItemEffect(ServerPlayer player, ItemType type)
    {
        switch (type)
        {
            case ItemType.HealthPack:
                player.Health = Math.Min(player.Health + 50, 100);
                break;

            case ItemType.ArmorPack:
                player.Armor = Math.Min(player.Armor + 50, 100);
                break;

            case ItemType.AmmoPack:
                // 모든 무기 탄약 보충
                foreach (var weapon in player.Ammunition.Keys.ToList())
                {
                    player.Ammunition[weapon] += 30;
                }
                break;
        }
    }
}

public class ItemSpawn
{
    public string Id { get; set; }
    public ItemType Type { get; set; }
    public Vector3 Position { get; set; }
    public bool IsActive { get; set; }
    public float RespawnTime { get; set; }
    public float RespawnTimer { get; set; }
}

public enum ItemType
{
    HealthPack,
    ArmorPack,
    AmmoPack
}
```

---

## 승리 조건

### FFA 모드
```csharp
public class FFAMode : GameMode
{
    private const int KILL_LIMIT = 20;

    public override bool CheckWinCondition()
    {
        // 킬 리밋 도달 확인
        var leader = GetLeader();

        if (leader != null && leader.Stats.Kills >= KILL_LIMIT)
        {
            Winner = leader;
            return true;
        }

        return false;
    }

    private ServerPlayer GetLeader()
    {
        return GameServer.AllPlayers
            .OrderByDescending(p => p.Stats.Kills)
            .FirstOrDefault();
    }

    public override void CalculateFinalRankings()
    {
        var rankings = GameServer.AllPlayers
            .OrderByDescending(p => p.Stats.Kills)
            .ThenBy(p => p.Stats.Deaths)
            .ToList();

        for (int i = 0; i < rankings.Count; i++)
        {
            rankings[i].FinalRank = i + 1;
        }
    }
}
```

### TDM 모드
```csharp
public class TDMMode : GameMode
{
    private const int TEAM_KILL_LIMIT = 50;

    public override bool CheckWinCondition()
    {
        var redKills = GetTeamKills(1);
        var blueKills = GetTeamKills(2);

        if (redKills >= TEAM_KILL_LIMIT)
        {
            WinningTeam = 1;
            return true;
        }

        if (blueKills >= TEAM_KILL_LIMIT)
        {
            WinningTeam = 2;
            return true;
        }

        return false;
    }

    private int GetTeamKills(byte team)
    {
        return GameServer.AllPlayers
            .Where(p => p.Team == team)
            .Sum(p => p.Stats.Kills);
    }

    public override void CalculateFinalRankings()
    {
        var redKills = GetTeamKills(1);
        var blueKills = GetTeamKills(2);

        byte winningTeam = redKills > blueKills ? (byte)1 : (byte)2;

        // 개인 순위 (팀 내)
        foreach (byte team in new[] { (byte)1, (byte)2 })
        {
            var teamPlayers = GameServer.AllPlayers
                .Where(p => p.Team == team)
                .OrderByDescending(p => p.Stats.Kills)
                .ToList();

            for (int i = 0; i < teamPlayers.Count; i++)
            {
                teamPlayers[i].FinalRank = i + 1;
                teamPlayers[i].TeamResult = (team == winningTeam) ? "win" : "loss";
            }
        }
    }
}
```

---

## 킬 스트릭

```csharp
public class KillstreakSystem
{
    public void OnPlayerKill(ServerPlayer killer, ServerPlayer victim)
    {
        killer.Stats.CurrentKillstreak++;

        if (killer.Stats.CurrentKillstreak > killer.Stats.HighestKillstreak)
        {
            killer.Stats.HighestKillstreak = killer.Stats.CurrentKillstreak;
        }

        // 킬 스트릭 보상
        CheckKillstreakReward(killer);

        // 피해자 스트릭 리셋
        victim.Stats.CurrentKillstreak = 0;
    }

    private void CheckKillstreakReward(ServerPlayer player)
    {
        switch (player.Stats.CurrentKillstreak)
        {
            case 5:
                AnnounceKillstreak(player, "Killing Spree!");
                break;

            case 10:
                AnnounceKillstreak(player, "Rampage!");
                break;

            case 15:
                AnnounceKillstreak(player, "Dominating!");
                break;

            case 20:
                AnnounceKillstreak(player, "Unstoppable!");
                break;
        }
    }

    private void AnnounceKillstreak(ServerPlayer player, string message)
    {
        BroadcastAnnouncement($"{player.Username}: {message}");
    }
}
```

완성되었습니다! FPS 게임 서버를 위한 6개의 상세 문서를 작성했습니다.
