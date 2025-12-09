# FPS 히트 판정 시스템

## 개요

서버 권한 기반 히트 판정 시스템으로, 클라이언트 렉을 보상하면서도 공정한 히트 판정을 제공합니다.

---

## 기본 원칙

### 1. 서버 권한 (Server Authority)
- 모든 히트 판정은 서버에서만 수행
- 클라이언트는 발사 정보만 전송
- 결과는 서버가 결정하고 모든 클라이언트에 브로드캐스트

### 2. 렉 보상 (Lag Compensation)
- 클라이언트의 지연 시간을 고려
- 발사 시점의 과거 플레이어 위치를 기반으로 히트 판정
- "클라이언트가 본 것이 진실"

### 3. 히트박스 기반 판정
- 신체 부위별 히트박스
- 부위별 데미지 배율 적용

---

## 히트박스 정의

### 히트박스 구조
```csharp
public class Hitbox
{
    public string Name { get; set; }         // 부위 이름
    public Vector3 Center { get; set; }      // 중심점 (로컬 좌표)
    public Vector3 Size { get; set; }        // 크기 (너비, 높이, 깊이)
    public float DamageMultiplier { get; set; }  // 데미지 배율
}
```

### 기본 히트박스 설정
```csharp
public static readonly Hitbox[] DefaultHitboxes = new[]
{
    // Head - 헤드샷
    new Hitbox
    {
        Name = "head",
        Center = new Vector3(0, 1.7f, 0),    // 눈 높이
        Size = new Vector3(0.2f, 0.25f, 0.2f),
        DamageMultiplier = 2.5f
    },

    // Chest - 상체
    new Hitbox
    {
        Name = "chest",
        Center = new Vector3(0, 1.3f, 0),
        Size = new Vector3(0.4f, 0.5f, 0.3f),
        DamageMultiplier = 1.0f
    },

    // Stomach - 복부
    new Hitbox
    {
        Name = "stomach",
        Center = new Vector3(0, 0.9f, 0),
        Size = new Vector3(0.35f, 0.3f, 0.3f),
        DamageMultiplier = 0.9f
    },

    // Arms - 팔 (2개)
    new Hitbox
    {
        Name = "arm_left",
        Center = new Vector3(-0.3f, 1.2f, 0),
        Size = new Vector3(0.15f, 0.6f, 0.15f),
        DamageMultiplier = 0.75f
    },
    new Hitbox
    {
        Name = "arm_right",
        Center = new Vector3(0.3f, 1.2f, 0),
        Size = new Vector3(0.15f, 0.6f, 0.15f),
        DamageMultiplier = 0.75f
    },

    // Legs - 다리 (2개)
    new Hitbox
    {
        Name = "leg_left",
        Center = new Vector3(-0.15f, 0.45f, 0),
        Size = new Vector3(0.15f, 0.9f, 0.15f),
        DamageMultiplier = 0.75f
    },
    new Hitbox
    {
        Name = "leg_right",
        Center = new Vector3(0.15f, 0.45f, 0),
        Size = new Vector3(0.15f, 0.9f, 0.15f),
        DamageMultiplier = 0.75f
    }
};
```

---

## 렉 보상 시스템

### 플레이어 히스토리 기록

#### 히스토리 스냅샷
```csharp
public class PlayerSnapshot
{
    public uint Timestamp { get; set; }     // 서버 시간 (ms)
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public bool IsAlive { get; set; }
    public bool IsCrouching { get; set; }
}
```

#### 히스토리 버퍼
```csharp
public class PlayerHistory
{
    private const int MAX_HISTORY_SIZE = 64;  // 1초치 (64 tick)
    private readonly Queue<PlayerSnapshot> snapshots = new();

    public void RecordSnapshot(PlayerSnapshot snapshot)
    {
        snapshots.Enqueue(snapshot);

        // 최대 크기 유지
        while (snapshots.Count > MAX_HISTORY_SIZE)
        {
            snapshots.Dequeue();
        }
    }

    public PlayerSnapshot GetSnapshotAtTime(uint targetTime)
    {
        if (snapshots.Count == 0)
            return null;

        PlayerSnapshot previous = null;
        PlayerSnapshot next = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Timestamp <= targetTime)
            {
                previous = snapshot;
            }
            else
            {
                next = snapshot;
                break;
            }
        }

        // 타겟 시간이 범위를 벗어나면
        if (previous == null)
            return snapshots.First();
        if (next == null)
            return snapshots.Last();

        // 선형 보간
        float t = (float)(targetTime - previous.Timestamp) /
                  (next.Timestamp - previous.Timestamp);

        return new PlayerSnapshot
        {
            Timestamp = targetTime,
            Position = Vector3.Lerp(previous.Position, next.Position, t),
            Rotation = Vector3.Lerp(previous.Rotation, next.Rotation, t),
            IsAlive = previous.IsAlive,
            IsCrouching = previous.IsCrouching
        };
    }
}
```

---

## Hitscan 히트 판정

### Raycast 기반 히트 판정

```csharp
public class HitDetection
{
    public HitResult PerformHitscan(
        Player shooter,
        Vector3 firePosition,
        Vector3 fireDirection,
        float maxRange,
        uint clientTimestamp)
    {
        // 1. 발사자의 Ping 계산
        int ping = shooter.Connection.Ping;

        // 2. 렉 보상: 클라이언트가 본 시점 계산
        uint lagCompensationTime = clientTimestamp - (uint)ping;

        // 3. 모든 플레이어를 과거 위치로 되돌림
        var playerStates = new Dictionary<Player, PlayerSnapshot>();
        foreach (var player in GameServer.AllPlayers)
        {
            if (player == shooter || !player.IsAlive)
                continue;

            var snapshot = player.History.GetSnapshotAtTime(lagCompensationTime);
            playerStates[player] = snapshot;
        }

        // 4. Raycast 수행
        HitResult closestHit = null;
        float closestDistance = maxRange;

        foreach (var kvp in playerStates)
        {
            var player = kvp.Key;
            var snapshot = kvp.Value;

            // 각 히트박스 검사
            foreach (var hitbox in player.Hitboxes)
            {
                var worldHitbox = TransformHitbox(hitbox, snapshot);

                if (RaycastBox(firePosition, fireDirection, worldHitbox, out float distance))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestHit = new HitResult
                        {
                            HitPlayer = player,
                            HitPosition = firePosition + fireDirection * distance,
                            HitboxName = hitbox.Name,
                            DamageMultiplier = hitbox.DamageMultiplier,
                            Distance = distance
                        };
                    }
                }
            }
        }

        return closestHit;
    }

    private Hitbox TransformHitbox(Hitbox hitbox, PlayerSnapshot snapshot)
    {
        // 히트박스를 월드 좌표로 변환
        var rotation = Quaternion.Euler(snapshot.Rotation);
        var worldCenter = snapshot.Position + rotation * hitbox.Center;

        // 웅크린 상태면 히트박스 조정
        if (snapshot.IsCrouching)
        {
            worldCenter.y -= 0.8f;  // 웅크림 오프셋
        }

        return new Hitbox
        {
            Name = hitbox.Name,
            Center = worldCenter,
            Size = hitbox.Size,
            DamageMultiplier = hitbox.DamageMultiplier
        };
    }

    private bool RaycastBox(Vector3 origin, Vector3 direction, Hitbox box, out float distance)
    {
        // AABB (Axis-Aligned Bounding Box) Raycast
        var min = box.Center - box.Size * 0.5f;
        var max = box.Center + box.Size * 0.5f;

        float tmin = 0.0f;
        float tmax = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(direction[i]) < 0.0001f)
            {
                // Ray is parallel to slab
                if (origin[i] < min[i] || origin[i] > max[i])
                {
                    distance = 0;
                    return false;
                }
            }
            else
            {
                float ood = 1.0f / direction[i];
                float t1 = (min[i] - origin[i]) * ood;
                float t2 = (max[i] - origin[i]) * ood;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                tmin = Math.Max(tmin, t1);
                tmax = Math.Min(tmax, t2);

                if (tmin > tmax)
                {
                    distance = 0;
                    return false;
                }
            }
        }

        distance = tmin;
        return tmin >= 0;
    }
}

public class HitResult
{
    public Player HitPlayer { get; set; }
    public Vector3 HitPosition { get; set; }
    public string HitboxName { get; set; }
    public float DamageMultiplier { get; set; }
    public float Distance { get; set; }
}
```

---

## Shotgun 히트 판정 (다중 펠릿)

```csharp
public class ShotgunHitDetection
{
    public List<HitResult> PerformShotgunBlast(
        Player shooter,
        Vector3 firePosition,
        Vector3 fireDirection,
        int pelletCount,
        float spreadAngle,
        float maxRange,
        uint clientTimestamp,
        uint spreadSeed)
    {
        var hits = new List<HitResult>();
        var random = new Random((int)spreadSeed);

        for (int i = 0; i < pelletCount; i++)
        {
            // 랜덤 스프레드 계산
            float yaw = (float)(random.NextDouble() - 0.5) * spreadAngle;
            float pitch = (float)(random.NextDouble() - 0.5) * spreadAngle;

            var pelletDirection = Quaternion.Euler(pitch, yaw, 0) * fireDirection;
            pelletDirection.Normalize();

            // 각 펠릿마다 Hitscan
            var hit = PerformHitscan(
                shooter,
                firePosition,
                pelletDirection,
                maxRange,
                clientTimestamp
            );

            if (hit != null)
            {
                hits.Add(hit);
            }
        }

        return hits;
    }
}
```

---

## Projectile 히트 판정

### 투사체 물리
```csharp
public class Projectile
{
    public uint Id { get; set; }
    public Player Owner { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Gravity { get; set; } = 9.81f;
    public float Radius { get; set; } = 0.1f;
    public int Damage { get; set; }
    public float SplashRadius { get; set; }
    public int SplashDamage { get; set; }

    public void Update(float deltaTime)
    {
        // 중력 적용
        Velocity.y -= Gravity * deltaTime;

        // 위치 업데이트
        var newPosition = Position + Velocity * deltaTime;

        // 충돌 검사
        if (CheckCollision(Position, newPosition, out var hitInfo))
        {
            OnHit(hitInfo);
        }
        else
        {
            Position = newPosition;
        }
    }

    private bool CheckCollision(Vector3 from, Vector3 to, out HitInfo hitInfo)
    {
        hitInfo = null;

        // 플레이어 충돌
        foreach (var player in GameServer.AllPlayers)
        {
            if (player == Owner || !player.IsAlive)
                continue;

            // 구체 vs 히트박스 충돌
            foreach (var hitbox in player.Hitboxes)
            {
                if (SphereCastHitbox(from, to, Radius, hitbox))
                {
                    hitInfo = new HitInfo
                    {
                        HitPlayer = player,
                        HitPosition = to,
                        Hitbox = hitbox
                    };
                    return true;
                }
            }
        }

        // 맵 충돌
        if (RaycastTerrain(from, to, out var terrainHit))
        {
            hitInfo = new HitInfo
            {
                HitPosition = terrainHit,
                IsTerrainHit = true
            };
            return true;
        }

        return false;
    }

    private void OnHit(HitInfo hitInfo)
    {
        if (hitInfo.HitPlayer != null)
        {
            // 직격 데미지
            ApplyDamage(hitInfo.HitPlayer, Damage * hitInfo.Hitbox.DamageMultiplier);
        }

        // 스플래시 데미지
        if (SplashRadius > 0)
        {
            ApplySplashDamage(hitInfo.HitPosition);
        }

        // 투사체 제거
        GameServer.RemoveProjectile(this);
    }

    private void ApplySplashDamage(Vector3 center)
    {
        foreach (var player in GameServer.AllPlayers)
        {
            if (!player.IsAlive)
                continue;

            float distance = Vector3.Distance(center, player.Position);

            if (distance <= SplashRadius)
            {
                // 거리에 따른 감쇠
                float falloff = 1.0f - (distance / SplashRadius);
                int damage = (int)(SplashDamage * falloff);

                ApplyDamage(player, damage);
            }
        }
    }
}
```

---

## 데미지 적용

```csharp
public class DamageSystem
{
    public void ApplyDamage(Player victim, Player attacker, int baseDamage, float multiplier, string hitboxName)
    {
        if (!victim.IsAlive)
            return;

        // 최종 데미지 계산
        int finalDamage = (int)(baseDamage * multiplier);

        // 아머 적용 (선택사항)
        if (victim.Armor > 0)
        {
            int armorAbsorb = Math.Min(victim.Armor, finalDamage / 2);
            victim.Armor -= armorAbsorb;
            finalDamage -= armorAbsorb;
        }

        // 체력 감소
        victim.Health -= finalDamage;

        // 데미지 이벤트 기록
        var damageEvent = new DamageEvent
        {
            Victim = victim,
            Attacker = attacker,
            Damage = finalDamage,
            HitboxName = hitboxName,
            IsHeadshot = hitboxName == "head",
            Timestamp = GameServer.CurrentTime
        };

        GameServer.LogDamageEvent(damageEvent);

        // 사망 처리
        if (victim.Health <= 0)
        {
            OnPlayerDeath(victim, attacker, damageEvent);
        }
    }

    private void OnPlayerDeath(Player victim, Player attacker, DamageEvent damageEvent)
    {
        victim.IsAlive = false;
        victim.Health = 0;

        // 킬/데스 통계 업데이트
        if (attacker != null && attacker != victim)
        {
            attacker.Stats.Kills++;
            victim.Stats.Deaths++;

            if (damageEvent.IsHeadshot)
            {
                attacker.Stats.Headshots++;
            }
        }
        else
        {
            // 자살
            victim.Stats.Deaths++;
        }

        // 모든 클라이언트에 사망 알림
        GameServer.BroadcastPlayerDeath(damageEvent);

        // 리스폰 타이머 시작
        victim.RespawnTimer = 3.0f;  // 3초 후 리스폰
    }
}
```

---

## 히트 판정 디버깅

### 서버 로그
```csharp
public void LogHitDetection(HitResult hit, Player shooter, uint timestamp)
{
    Console.WriteLine($"[HIT] {shooter.Username} → {hit.HitPlayer.Username}");
    Console.WriteLine($"  Hitbox: {hit.HitboxName} (x{hit.DamageMultiplier})");
    Console.WriteLine($"  Distance: {hit.Distance:F2}m");
    Console.WriteLine($"  Timestamp: {timestamp} (Lag: {shooter.Connection.Ping}ms)");
    Console.WriteLine($"  Position: {hit.HitPosition}");
}
```

### 클라이언트 피드백
```csharp
// 히트 마커 표시
public void ShowHitMarker(bool isHeadshot)
{
    if (isHeadshot)
    {
        UI.ShowHeadshotMarker();
        Audio.PlaySound("headshot_hit");
    }
    else
    {
        UI.ShowHitMarker();
        Audio.PlaySound("body_hit");
    }
}
```

---

## 치트 방지

### 히트 판정 검증
```csharp
public bool ValidateHit(Player shooter, HitResult hit)
{
    // 1. 거리 검증
    float actualDistance = Vector3.Distance(shooter.Position, hit.HitPosition);
    if (actualDistance > shooter.CurrentWeapon.MaxRange)
    {
        LogCheatAttempt(shooter, "Distance check failed");
        return false;
    }

    // 2. 시야각 검증 (±90도 이내)
    var shooterForward = Quaternion.Euler(shooter.Rotation) * Vector3.forward;
    var toTarget = (hit.HitPosition - shooter.Position).Normalized();
    float angle = Vector3.Angle(shooterForward, toTarget);

    if (angle > 90)
    {
        LogCheatAttempt(shooter, "Aim angle check failed");
        return false;
    }

    // 3. 발사 속도 검증
    if (!shooter.Weapon.CanFire())
    {
        LogCheatAttempt(shooter, "Fire rate check failed");
        return false;
    }

    return true;
}
```

---

## 성능 최적화

### 공간 분할 (Spatial Partitioning)
```csharp
public class SpatialGrid
{
    private Dictionary<Vector2Int, List<Player>> grid = new();
    private float cellSize = 10.0f;  // 10m x 10m 셀

    public void UpdatePlayer(Player player)
    {
        var cell = WorldToCell(player.Position);

        // 기존 셀에서 제거
        if (player.CurrentCell != cell)
        {
            if (grid.TryGetValue(player.CurrentCell, out var oldList))
            {
                oldList.Remove(player);
            }

            // 새 셀에 추가
            if (!grid.TryGetValue(cell, out var newList))
            {
                newList = new List<Player>();
                grid[cell] = newList;
            }

            newList.Add(player);
            player.CurrentCell = cell;
        }
    }

    public List<Player> GetNearbyPlayers(Vector3 position, float radius)
    {
        var result = new List<Player>();
        var center = WorldToCell(position);
        int cellRadius = (int)Math.Ceiling(radius / cellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                var cell = new Vector2Int(center.x + x, center.y + z);

                if (grid.TryGetValue(cell, out var players))
                {
                    result.AddRange(players);
                }
            }
        }

        return result;
    }

    private Vector2Int WorldToCell(Vector3 position)
    {
        return new Vector2Int(
            (int)Math.Floor(position.x / cellSize),
            (int)Math.Floor(position.z / cellSize)
        );
    }
}
```

이렇게 공간 분할을 사용하면 히트 판정 시 모든 플레이어를 검사하지 않고, 가까운 플레이어만 검사할 수 있습니다.
