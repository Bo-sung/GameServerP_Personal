# FPS 데이터베이스 확장

이 문서는 상위 프레임워크의 데이터베이스 구조를 FPS 게임에 맞게 확장하는 방법을 설명합니다.

---

## 기본 원칙

### 프레임워크 활용
상위 프레임워크에서 제공하는 `game_records` 테이블의 `stats` JSON 필드를 활용하여 FPS 특화 데이터를 저장합니다.

### 추가 테이블
FPS 게임에만 필요한 복잡한 데이터는 별도 테이블로 관리합니다.

---

## MySQL 확장

### 1. 무기 통계 테이블
```sql
CREATE TABLE fps_weapon_stats (
    stat_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    weapon_id VARCHAR(50) NOT NULL,

    -- 통계
    kills INT DEFAULT 0,
    deaths INT DEFAULT 0,
    shots_fired INT DEFAULT 0,
    shots_hit INT DEFAULT 0,
    headshots INT DEFAULT 0,
    damage_dealt BIGINT DEFAULT 0,

    -- 정확도 (계산)
    accuracy FLOAT GENERATED ALWAYS AS (
        CASE WHEN shots_fired > 0
        THEN (shots_hit / shots_fired) * 100
        ELSE 0 END
    ) STORED,

    -- 시간
    last_updated DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    UNIQUE KEY unique_user_weapon (user_id, weapon_id),
    INDEX idx_user_id (user_id),
    INDEX idx_weapon_id (weapon_id),
    INDEX idx_kills (kills DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 2. 맵 통계 테이블
```sql
CREATE TABLE fps_map_stats (
    stat_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    map_id VARCHAR(50) NOT NULL,

    -- 전적
    games_played INT DEFAULT 0,
    wins INT DEFAULT 0,
    losses INT DEFAULT 0,
    kills INT DEFAULT 0,
    deaths INT DEFAULT 0,

    -- 승률
    win_rate FLOAT GENERATED ALWAYS AS (
        CASE WHEN games_played > 0
        THEN (wins / games_played) * 100
        ELSE 0 END
    ) STORED,

    -- K/D
    kd_ratio FLOAT GENERATED ALWAYS AS (
        CASE WHEN deaths > 0
        THEN kills / deaths
        ELSE kills END
    ) STORED,

    -- 시간
    last_played DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    UNIQUE KEY unique_user_map (user_id, map_id),
    INDEX idx_user_id (user_id),
    INDEX idx_map_id (map_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 3. 킬 피드 로그 (선택사항, 분석용)
```sql
CREATE TABLE fps_kill_logs (
    log_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    session_id VARCHAR(36) NOT NULL,

    -- 킬 정보
    killer_id VARCHAR(36),
    victim_id VARCHAR(36) NOT NULL,
    weapon_id VARCHAR(50) NOT NULL,
    hitbox VARCHAR(20) NOT NULL,
    is_headshot BOOLEAN DEFAULT FALSE,

    -- 위치 정보
    killer_position_x FLOAT,
    killer_position_y FLOAT,
    killer_position_z FLOAT,
    victim_position_x FLOAT,
    victim_position_y FLOAT,
    victim_position_z FLOAT,
    distance FLOAT,

    -- 시간
    game_time INT NOT NULL COMMENT '게임 내 경과 시간 (초)',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    INDEX idx_session_id (session_id),
    INDEX idx_killer_id (killer_id),
    INDEX idx_victim_id (victim_id),
    INDEX idx_created_at (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
PARTITION BY RANGE (YEAR(created_at)) (
    PARTITION p2025 VALUES LESS THAN (2026),
    PARTITION p2026 VALUES LESS THAN (2027),
    PARTITION p_future VALUES LESS THAN MAXVALUE
);
```

### 4. 유저 FPS 통계 확장
```sql
-- user_stats 테이블에 FPS 전용 컬럼 추가
ALTER TABLE user_stats
ADD COLUMN fps_total_kills INT DEFAULT 0,
ADD COLUMN fps_total_deaths INT DEFAULT 0,
ADD COLUMN fps_total_headshots INT DEFAULT 0,
ADD COLUMN fps_total_shots_fired BIGINT DEFAULT 0,
ADD COLUMN fps_total_shots_hit BIGINT DEFAULT 0,
ADD COLUMN fps_favorite_weapon VARCHAR(50),
ADD COLUMN fps_kd_ratio FLOAT GENERATED ALWAYS AS (
    CASE WHEN fps_total_deaths > 0
    THEN fps_total_kills / fps_total_deaths
    ELSE fps_total_kills END
) STORED,
ADD COLUMN fps_accuracy FLOAT GENERATED ALWAYS AS (
    CASE WHEN fps_total_shots_fired > 0
    THEN (fps_total_shots_hit / fps_total_shots_fired) * 100
    ELSE 0 END
) STORED;

-- 인덱스 추가
CREATE INDEX idx_fps_kd_ratio ON user_stats(fps_kd_ratio DESC);
CREATE INDEX idx_fps_kills ON user_stats(fps_total_kills DESC);
```

---

## game_records의 stats JSON 활용

### FPS 게임 기록 저장 형식
```json
{
  "game_mode": "ffa",
  "map": "map_industrial",
  "kills": 15,
  "deaths": 8,
  "assists": 0,
  "headshots": 5,
  "accuracy": 42.5,
  "damage_dealt": 2500,
  "damage_taken": 1800,
  "distance_traveled": 1200,
  "shots_fired": 250,
  "shots_hit": 106,
  "weapon_stats": {
    "ar_default": {
      "kills": 10,
      "shots_fired": 180,
      "shots_hit": 78,
      "headshots": 3
    },
    "sniper_default": {
      "kills": 5,
      "shots_fired": 20,
      "shots_hit": 8,
      "headshots": 2
    }
  },
  "highest_killstreak": 7,
  "final_rank": 1
}
```

### C# 데이터 구조
```csharp
public class FPSGameStats
{
    [JsonPropertyName("game_mode")]
    public string GameMode { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("assists")]
    public int Assists { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }

    [JsonPropertyName("accuracy")]
    public float Accuracy { get; set; }

    [JsonPropertyName("damage_dealt")]
    public int DamageDealt { get; set; }

    [JsonPropertyName("damage_taken")]
    public int DamageTaken { get; set; }

    [JsonPropertyName("distance_traveled")]
    public float DistanceTraveled { get; set; }

    [JsonPropertyName("shots_fired")]
    public int ShotsFired { get; set; }

    [JsonPropertyName("shots_hit")]
    public int ShotsHit { get; set; }

    [JsonPropertyName("weapon_stats")]
    public Dictionary<string, WeaponStats> WeaponStats { get; set; }

    [JsonPropertyName("highest_killstreak")]
    public int HighestKillstreak { get; set; }

    [JsonPropertyName("final_rank")]
    public int FinalRank { get; set; }
}

public class WeaponStats
{
    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("shots_fired")]
    public int ShotsFired { get; set; }

    [JsonPropertyName("shots_hit")]
    public int ShotsHit { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }
}
```

### 게임 종료 시 저장
```csharp
public async Task SaveGameRecord(Player player, FPSGameStats stats)
{
    var statsJson = JsonSerializer.Serialize(stats);

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    // game_records 테이블에 저장
    var query = @"
        INSERT INTO game_records (
            user_id, session_id, game_mode, result, score, stats,
            started_at, ended_at, duration_seconds
        ) VALUES (
            @userId, @sessionId, @gameMode, @result, @score, @stats,
            @startedAt, @endedAt, @duration
        )";

    await connection.ExecuteAsync(query, new
    {
        userId = player.UserId,
        sessionId = player.SessionId,
        gameMode = stats.GameMode,
        result = stats.FinalRank == 1 ? "win" : "lose",
        score = stats.Kills,
        stats = statsJson,
        startedAt = player.GameStartTime,
        endedAt = DateTime.UtcNow,
        duration = (int)(DateTime.UtcNow - player.GameStartTime).TotalSeconds
    });

    // 통계 업데이트
    await UpdateUserStats(player, stats);
}
```

---

## Redis 확장

### 1. 실시간 킬 피드
```
키 패턴: game:{session_id}:killfeed
타입: List (최신 순)
TTL: 게임 종료 시 삭제
```

```redis
# 킬 이벤트 추가
LPUSH game:session123:killfeed '{"killer":"player1","victim":"player2","weapon":"ar_default","headshot":true,"time":45}'

# 최근 10개 킬 피드 조회
LRANGE game:session123:killfeed 0 9

# 게임 종료 시 삭제
DEL game:session123:killfeed
```

### 2. 플레이어 현재 상태
```
키 패턴: game:{session_id}:player:{player_id}
타입: Hash
TTL: 게임 종료 후 10분
```

```redis
HMSET game:session123:player:user1 \
    kills 5 \
    deaths 2 \
    health 75 \
    weapon "ar_default" \
    position_x 10.5 \
    position_y 0 \
    position_z 20.3

# 킬 수 증가
HINCRBY game:session123:player:user1 kills 1
```

### 3. 리더보드 (실시간)
```
키 패턴: game:{session_id}:leaderboard
타입: Sorted Set
Score: 킬 수
TTL: 게임 종료 후 10분
```

```redis
# 점수 업데이트 (킬 발생 시)
ZADD game:session123:leaderboard 6 "player1"

# 리더보드 조회 (상위 10명)
ZREVRANGE game:session123:leaderboard 0 9 WITHSCORES

# 특정 플레이어 순위
ZREVRANK game:session123:leaderboard "player1"
```

---

## 통계 업데이트

### 전체 통계 업데이트
```csharp
public async Task UpdateUserStats(Player player, FPSGameStats stats)
{
    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync();

    // user_stats 업데이트
    await connection.ExecuteAsync(@"
        UPDATE user_stats
        SET fps_total_kills = fps_total_kills + @kills,
            fps_total_deaths = fps_total_deaths + @deaths,
            fps_total_headshots = fps_total_headshots + @headshots,
            fps_total_shots_fired = fps_total_shots_fired + @shotsFired,
            fps_total_shots_hit = fps_total_shots_hit + @shotsHit
        WHERE user_id = @userId
    ", new
    {
        userId = player.UserId,
        kills = stats.Kills,
        deaths = stats.Deaths,
        headshots = stats.Headshots,
        shotsFired = stats.ShotsFired,
        shotsHit = stats.ShotsHit
    });

    // 무기별 통계 업데이트
    foreach (var weapon in stats.WeaponStats)
    {
        await connection.ExecuteAsync(@"
            INSERT INTO fps_weapon_stats (
                user_id, weapon_id, kills, shots_fired, shots_hit, headshots
            ) VALUES (
                @userId, @weaponId, @kills, @shotsFired, @shotsHit, @headshots
            )
            ON DUPLICATE KEY UPDATE
                kills = kills + @kills,
                shots_fired = shots_fired + @shotsFired,
                shots_hit = shots_hit + @shotsHit,
                headshots = headshots + @headshots
        ", new
        {
            userId = player.UserId,
            weaponId = weapon.Key,
            kills = weapon.Value.Kills,
            shotsFired = weapon.Value.ShotsFired,
            shotsHit = weapon.Value.ShotsHit,
            headshots = weapon.Value.Headshots
        });
    }

    // 맵별 통계 업데이트
    var result = stats.FinalRank == 1 ? "win" : "loss";
    await connection.ExecuteAsync(@"
        INSERT INTO fps_map_stats (
            user_id, map_id, games_played, wins, losses, kills, deaths
        ) VALUES (
            @userId, @mapId, 1,
            @wins, @losses, @kills, @deaths
        )
        ON DUPLICATE KEY UPDATE
            games_played = games_played + 1,
            wins = wins + @wins,
            losses = losses + @losses,
            kills = kills + @kills,
            deaths = deaths + @deaths
    ", new
    {
        userId = player.UserId,
        mapId = stats.Map,
        wins = result == "win" ? 1 : 0,
        losses = result == "loss" ? 1 : 0,
        kills = stats.Kills,
        deaths = stats.Deaths
    });
}
```

---

## 리더보드 쿼리

### 글로벌 킬 리더보드
```sql
SELECT
    u.user_id,
    u.username,
    us.fps_total_kills AS kills,
    us.fps_total_deaths AS deaths,
    us.fps_kd_ratio AS kd_ratio,
    us.fps_total_headshots AS headshots
FROM user_stats us
JOIN users u ON us.user_id = u.user_id
ORDER BY us.fps_total_kills DESC
LIMIT 100;
```

### 무기별 리더보드
```sql
SELECT
    u.username,
    ws.weapon_id,
    ws.kills,
    ws.accuracy
FROM fps_weapon_stats ws
JOIN users u ON ws.user_id = u.user_id
WHERE ws.weapon_id = 'sniper_default'
ORDER BY ws.kills DESC
LIMIT 100;
```

### 맵별 승률 리더보드
```sql
SELECT
    u.username,
    ms.map_id,
    ms.games_played,
    ms.win_rate,
    ms.kd_ratio
FROM fps_map_stats ms
JOIN users u ON ms.user_id = u.user_id
WHERE ms.map_id = 'map_industrial' AND ms.games_played >= 10
ORDER BY ms.win_rate DESC
LIMIT 100;
```

---

## 데이터 분석 쿼리

### 플레이어 프로필 통계
```sql
-- 플레이어의 전체 프로필
SELECT
    u.username,
    u.level,
    us.total_games,
    us.wins,
    us.losses,
    us.fps_total_kills,
    us.fps_total_deaths,
    us.fps_kd_ratio,
    us.fps_accuracy,
    us.fps_total_headshots,
    us.fps_favorite_weapon
FROM users u
JOIN user_stats us ON u.user_id = us.user_id
WHERE u.user_id = ?;
```

### 무기 선호도 분석
```sql
-- 플레이어의 무기 사용 통계
SELECT
    weapon_id,
    kills,
    shots_fired,
    shots_hit,
    accuracy,
    headshots,
    (headshots / kills * 100) AS headshot_rate
FROM fps_weapon_stats
WHERE user_id = ?
ORDER BY kills DESC;
```

### 최근 게임 기록
```sql
-- 최근 10게임
SELECT
    gr.game_mode,
    gr.result,
    gr.score,
    JSON_EXTRACT(gr.stats, '$.map') AS map,
    JSON_EXTRACT(gr.stats, '$.kills') AS kills,
    JSON_EXTRACT(gr.stats, '$.deaths') AS deaths,
    JSON_EXTRACT(gr.stats, '$.accuracy') AS accuracy,
    gr.started_at,
    gr.duration_seconds
FROM game_records gr
WHERE gr.user_id = ?
ORDER BY gr.started_at DESC
LIMIT 10;
```

---

## 백업 및 유지보수

### 데이터 보관 정책
```sql
-- 90일 이상 오래된 킬 로그 삭제 (분석 후)
DELETE FROM fps_kill_logs
WHERE created_at < DATE_SUB(NOW(), INTERVAL 90 DAY);

-- 비활성 유저 통계 아카이빙
CREATE TABLE fps_weapon_stats_archive LIKE fps_weapon_stats;

INSERT INTO fps_weapon_stats_archive
SELECT * FROM fps_weapon_stats ws
WHERE ws.user_id IN (
    SELECT user_id FROM users
    WHERE last_login < DATE_SUB(NOW(), INTERVAL 1 YEAR)
);
```

### 인덱스 최적화
```sql
-- 사용되지 않는 인덱스 확인
SELECT * FROM sys.schema_unused_indexes
WHERE object_schema = 'game_db';

-- 중복 인덱스 확인
SELECT * FROM sys.schema_redundant_indexes
WHERE table_schema = 'game_db';
```
