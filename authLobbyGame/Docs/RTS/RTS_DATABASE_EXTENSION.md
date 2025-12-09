# RTS 데이터베이스 확장 (RTS Database Extension)

## 1. 개요

RTS 게임을 지원하기 위한 MySQL과 Redis의 데이터 구조 확장.

### 1.1 설계 원칙
```
MySQL:
- 영구적인 데이터만 저장
- 게임 결과, 통계, 플레이어 기록
- 트랜잭션 보장 필요

Redis:
- 실시간 게임 상태 저장
- 고속 접근 필요
- 자동 만료 (TTL) 설정
```

---

## 2. MySQL 테이블 설계

### 2.1 게임 맵 정보 (maps)
```sql
CREATE TABLE maps (
    map_id INT PRIMARY KEY AUTO_INCREMENT,
    map_name VARCHAR(100) NOT NULL,
    map_type ENUM('1v1', '2v2', '3v3', 'ffa') NOT NULL,
    width INT NOT NULL,
    height INT NOT NULL,
    tile_size INT DEFAULT 32,
    description TEXT,
    terrain_file VARCHAR(255),
    resource_file VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    KEY idx_map_type (map_type),
    KEY idx_created_at (created_at)
);
```

**설명**:
- `map_id`: 맵 고유 ID
- `map_type`: 게임 타입 (1v1, 2v2 등)
- `terrain_file`: 지형 데이터 파일 경로
- `resource_file`: 자원 위치 데이터 파일 경로

### 2.2 게임 세션 (game_sessions)
```sql
CREATE TABLE game_sessions (
    game_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    map_id INT NOT NULL,
    game_mode VARCHAR(20) NOT NULL,
    player_count INT NOT NULL,
    start_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    end_time TIMESTAMP NULL,
    duration_seconds INT NULL,
    status ENUM('waiting', 'in_progress', 'completed') DEFAULT 'waiting',
    winner_player_id INT NULL,
    is_tournament BOOLEAN DEFAULT FALSE,
    
    FOREIGN KEY (map_id) REFERENCES maps(map_id),
    KEY idx_status (status),
    KEY idx_start_time (start_time),
    KEY idx_winner_player_id (winner_player_id)
);
```

**설명**:
- `game_id`: 게임 고유 ID
- `status`: 게임 상태 추적
- `duration_seconds`: 게임 진행 시간
- `is_tournament`: 토너먼트 게임 여부

### 2.3 게임 플레이어 참여 (game_players)
```sql
CREATE TABLE game_players (
    id INT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    player_id INT NOT NULL,
    team INT,
    start_position_x INT,
    start_position_y INT,
    color VARCHAR(7),
    initial_minerals INT DEFAULT 500,
    initial_gas INT DEFAULT 100,
    initial_food INT DEFAULT 15,
    is_defeated BOOLEAN DEFAULT FALSE,
    defeat_time INT NULL,
    result ENUM('victory', 'defeat', 'surrender', 'disconnect') NULL,
    
    FOREIGN KEY (game_id) REFERENCES game_sessions(game_id),
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    UNIQUE KEY unique_game_player (game_id, player_id),
    KEY idx_team (team),
    KEY idx_result (result)
);
```

**설명**:
- `team`: 팀 번호 (null이면 1v1)
- `defeat_time`: 패배까지 경과 시간
- `is_defeated`: 게임 중 패배 여부 추적

### 2.4 게임 통계 (game_statistics)
```sql
CREATE TABLE game_statistics (
    id INT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    player_id INT NOT NULL,
    
    -- 자원 관련
    minerals_gathered INT DEFAULT 0,
    gas_gathered INT DEFAULT 0,
    food_produced INT DEFAULT 0,
    
    -- 유닛 관련
    units_created INT DEFAULT 0,
    units_killed INT DEFAULT 0,
    units_lost INT DEFAULT 0,
    
    -- 건물 관련
    buildings_created INT DEFAULT 0,
    buildings_destroyed INT DEFAULT 0,
    buildings_lost INT DEFAULT 0,
    
    -- 전투 통계
    damage_dealt INT DEFAULT 0,
    damage_taken INT DEFAULT 0,
    total_army_size INT DEFAULT 0,
    max_army_size INT DEFAULT 0,
    
    -- 기술 연구
    technologies_researched INT DEFAULT 0,
    upgrades_completed INT DEFAULT 0,
    
    -- 게임 플레이
    actions_per_minute INT DEFAULT 0,
    game_time_seconds INT DEFAULT 0,
    
    UNIQUE KEY unique_game_player_stat (game_id, player_id),
    FOREIGN KEY (game_id) REFERENCES game_sessions(game_id),
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    KEY idx_apm (actions_per_minute),
    KEY idx_units_created (units_created)
);
```

**설명**:
- 게임 진행 중 수집되는 상세 통계
- 플레이어 성능 분석용
- 밸런싱 데이터 수집

### 2.5 플레이어 총 통계 (player_rts_stats)
```sql
CREATE TABLE player_rts_stats (
    player_id INT PRIMARY KEY,
    
    -- 게임 수
    total_games INT DEFAULT 0,
    wins INT DEFAULT 0,
    losses INT DEFAULT 0,
    win_rate FLOAT DEFAULT 0,
    
    -- 평정 (Rating)
    elo_rating INT DEFAULT 1600,
    last_rating_update TIMESTAMP NULL,
    
    -- 누적 통계
    total_minerals_gathered BIGINT DEFAULT 0,
    total_gas_gathered BIGINT DEFAULT 0,
    total_units_created BIGINT DEFAULT 0,
    total_units_killed BIGINT DEFAULT 0,
    total_damage_dealt BIGINT DEFAULT 0,
    
    -- 최고 기록
    highest_apm INT DEFAULT 0,
    best_win_streak INT DEFAULT 0,
    current_win_streak INT DEFAULT 0,
    
    -- 업적
    achievements_unlocked INT DEFAULT 0,
    
    -- 업데이트
    last_game_time TIMESTAMP NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    KEY idx_elo_rating (elo_rating),
    KEY idx_win_rate (win_rate),
    KEY idx_last_game_time (last_game_time)
);
```

**설명**:
- 플레이어의 누적 통계
- ELO 레이팅 시스템
- 성취도 추적

### 2.6 리더보드 (leaderboards)
```sql
CREATE TABLE leaderboards (
    id INT PRIMARY KEY AUTO_INCREMENT,
    player_id INT NOT NULL,
    rank INT NOT NULL,
    leaderboard_type ENUM('elo', 'wins', 'apm', 'total_games') NOT NULL,
    points INT NOT NULL,
    season INT DEFAULT 1,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    UNIQUE KEY unique_player_type_season (player_id, leaderboard_type, season),
    KEY idx_rank (rank),
    KEY idx_season (season),
    KEY idx_leaderboard_type (leaderboard_type)
);
```

**설명**:
- 다양한 리더보드 유형 지원
- 시즌별 추적
- 실시간 순위 업데이트

### 2.7 업적 (achievements)
```sql
CREATE TABLE achievements (
    achievement_id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    icon_url VARCHAR(255),
    condition_type VARCHAR(50),
    condition_value INT,
    reward_points INT DEFAULT 10,
    
    KEY idx_name (name)
);

CREATE TABLE player_achievements (
    id INT PRIMARY KEY AUTO_INCREMENT,
    player_id INT NOT NULL,
    achievement_id INT NOT NULL,
    unlocked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    UNIQUE KEY unique_player_achievement (player_id, achievement_id),
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id),
    KEY idx_unlocked_at (unlocked_at)
);
```

**설명**:
- 게임 내 업적 시스템
- 플레이어 동기부여
- 진행상황 추적

### 2.8 매치 히스토리 (match_history)
```sql
CREATE TABLE match_history (
    id INT PRIMARY KEY AUTO_INCREMENT,
    game_id BIGINT NOT NULL,
    player_id INT NOT NULL,
    opponent_id INT,
    match_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    map_id INT,
    result ENUM('win', 'loss', 'draw') NOT NULL,
    game_duration INT,
    mmr_change INT DEFAULT 0,
    replay_file VARCHAR(255),
    
    FOREIGN KEY (game_id) REFERENCES game_sessions(game_id),
    FOREIGN KEY (player_id) REFERENCES users(user_id),
    FOREIGN KEY (opponent_id) REFERENCES users(user_id),
    KEY idx_player_id (player_id),
    KEY idx_match_date (match_date),
    KEY idx_result (result)
);
```

**설명**:
- 플레이어의 최근 게임 기록
- 빠른 조회용
- 리플레이 파일 저장

---

## 3. Redis 데이터 구조

### 3.1 활성 게임 정보
```
Key: game:{game_id}
Type: Hash
TTL: 게임 진행 시간 + 10분

Fields:
{
    "map_id": 1,
    "game_mode": "1v1",
    "status": "in_progress",
    "start_time": 1702154400000,
    "tick": 150,
    "players": "1,2",
    "server_id": "game_server_1"
}
```

**사용**: 게임 서버가 활성 게임 추적용

### 3.2 플레이어별 게임 상태
```
Key: game:{game_id}:player:{player_id}
Type: Hash
TTL: 게임 진행 시간 + 10분

Fields:
{
    "team": 1,
    "color": "FF0000",
    "resources": {
        "minerals": 650,
        "gas": 150,
        "food": 14
    },
    "population": {
        "current": 8,
        "max": 25
    },
    "score": 250,
    "is_defeated": false,
    "defeat_time": null,
    "position": {"x": 100, "y": 100}
}
```

**사용**: 실시간 플레이어 상태 조회

### 3.3 게임 맵 데이터
```
Key: map:{map_id}:terrain
Type: String (Binary)
TTL: 무한 (또는 매주 갱신)

Data: 지형 타일 데이터 (256x256 = 65536 bytes)
```

```
Key: map:{map_id}:resources
Type: Sorted Set
TTL: 무한

Members:
{
    "mineral_1": {"x": 150, "y": 150, "amount": 1500},
    "mineral_2": {"x": 200, "y": 200, "amount": 1200},
    "gas_1": {"x": 180, "y": 180, "amount": 500},
    ...
}
```

**사용**: 빠른 맵 로드, 자원 위치 조회

### 3.4 활성 유닛 정보
```
Key: game:{game_id}:units
Type: Sorted Set (정렬 기준: unit_id)
TTL: 게임 진행 시간 + 10분

Members:
{
    unit_1: {
        "player_id": 1,
        "type": "worker",
        "x": 250.5,
        "y": 180.3,
        "health": 45,
        "max_health": 50,
        "state": "moving",
        "target": 300,
        "speed": 2.5
    },
    unit_2: {...},
    ...
}
```

**사용**: 게임 중 유닛 상태 조회, 동기화

### 3.5 활성 건물 정보
```
Key: game:{game_id}:buildings
Type: Sorted Set
TTL: 게임 진행 시간 + 10분

Members:
{
    building_1: {
        "player_id": 1,
        "type": "barracks",
        "x": 100,
        "y": 100,
        "health": 200,
        "max_health": 300,
        "completion": 1.0,
        "production_queue": "infantry,archer",
        "current_research": null
    },
    ...
}
```

**사용**: 건물 상태 조회, 생산 큐 관리

### 3.6 명령 큐
```
Key: game:{game_id}:commands:{player_id}
Type: List (FIFO)
TTL: 100ms (자동 실행 후 삭제)

Elements:
[
    {
        "command_type": "move",
        "unit_ids": [1, 2, 3],
        "target_x": 200,
        "target_y": 200,
        "timestamp": 1702154401000
    },
    {
        "command_type": "attack",
        "unit_ids": [4],
        "target_unit_id": 10,
        "timestamp": 1702154401050
    }
]
```

**사용**: 플레이어 명령 큐 관리

### 3.7 포그 오브 워 맵
```
Key: game:{game_id}:fog:{player_id}
Type: String (Binary Bitmap)
TTL: 게임 진행 시간 + 10분

Data:
- 256x256 맵 = 65536 타일
- 각 타일 2비트 (unexplored=0, explored=1, visible=2)
- 총 크기: 16KB (또는 압축 시 4KB)
```

**사용**: 포그 오브 워 계산 및 동기화

### 3.8 게임 이벤트 로그
```
Key: game:{game_id}:events
Type: Sorted Set (정렬 기준: timestamp)
TTL: 게임 진행 시간 + 30분

Members:
{
    "1702154401000": {
        "event_type": "unit_killed",
        "unit_id": 15,
        "player_id": 1,
        "killer_id": 5,
        "killer_player_id": 2
    },
    "1702154402000": {
        "event_type": "building_created",
        "building_id": 10,
        "player_id": 1,
        "type": "barracks"
    },
    ...
}
```

**사용**: 게임 진행 추적, 관전자 업데이트, 리플레이

### 3.9 활성 플레이어 세션
```
Key: rts_session:{player_id}
Type: Hash
TTL: 30분 (또는 명시적 로그아웃까지)

Fields:
{
    "game_id": 12345,
    "token": "jwt_token_here",
    "connected_at": 1702154400000,
    "last_ping": 1702154402000,
    "latency_ms": 45,
    "is_spectating": false
}
```

**사용**: 플레이어 온라인 상태, 게임 배정 확인

### 3.10 관중 (Spectator) 추적
```
Key: game:{game_id}:spectators
Type: Set
TTL: 게임 진행 시간 + 10분

Members: [spectator_1, spectator_2, ...]
```

```
Key: spectator:{spectator_id}
Type: Hash
TTL: 30분

Fields:
{
    "watched_game_id": 12345,
    "camera_x": 200,
    "camera_y": 200,
    "zoom": 1.0,
    "following_player": 1,
    "connected_at": 1702154400000
}
```

**사용**: 관전자 정보 저장 및 조회

### 3.11 실시간 통계
```
Key: stats:realtime:{game_id}
Type: Hash
TTL: 게임 진행 시간 + 10분

Fields:
{
    "total_kills": 25,
    "total_buildings": 15,
    "average_apm": 150,
    "longest_game_duration": 3600,
    "total_resources_gathered": 5000
}
```

**사용**: 게임 중 통계 집계

### 3.12 리더보드 캐시
```
Key: leaderboard:{type}:{season}
Type: Sorted Set (Score 기준 역순)
TTL: 1시간

Members:
{
    player_1: 2500,  // ELO 점수
    player_2: 2400,
    player_3: 2350,
    ...
}
```

**사용**: 빠른 리더보드 조회

---

## 4. 데이터 관리 및 최적화

### 4.1 데이터 정리 (Cleanup)
```
# 30분 이상 지난 완료된 게임 아카이브
* 0,30 * * * * /scripts/archive_completed_games.sh

# 1시간 이상 오래된 활성 세션 정리
* 0 * * * * /scripts/cleanup_idle_sessions.sh

# 일주일 이상 오래된 이벤트 로그 압축
0 2 * * 0 /scripts/compress_old_event_logs.sh

# 월별 통계 롤업
0 3 1 * * /scripts/rollup_monthly_statistics.sh
```

### 4.2 백업 전략
```
MySQL:
- 일일 전체 백업 (오전 3시)
- 시간별 증분 백업
- 보존 기간: 30일

Redis:
- 주기적 스냅샷 (RDB)
  * 게임 완료 후
  * 매시간
  * 보존 기간: 7일
- AOF (Append Only File) 활성화

Replica 구성:
- Redis Sentinel로 고가용성
- MySQL 읽기 복제본 (Read Replicas)
```

### 4.3 색인 최적화
```sql
-- 자주 조회하는 쿼리 최적화
ALTER TABLE game_statistics ADD INDEX idx_game_id_player_id (game_id, player_id);
ALTER TABLE match_history ADD INDEX idx_player_date (player_id, match_date);
ALTER TABLE leaderboards ADD INDEX idx_season_type_rank (season, leaderboard_type, rank);
```

### 4.4 파티셔닝
```sql
-- 큰 테이블 파티셔닝 (월별)
ALTER TABLE game_statistics
PARTITION BY RANGE (YEAR(created_at)*100 + MONTH(created_at)) (
    PARTITION p202401 VALUES LESS THAN (202402),
    PARTITION p202402 VALUES LESS THAN (202403),
    ...
);
```

---

## 5. 쿼리 예제

### 5.1 게임 결과 저장
```sql
-- 1. 게임 완료
UPDATE game_sessions 
SET status = 'completed', 
    end_time = NOW(),
    duration_seconds = UNIX_TIMESTAMP(NOW()) - UNIX_TIMESTAMP(start_time),
    winner_player_id = 1
WHERE game_id = 12345;

-- 2. 플레이어 결과 저장
UPDATE game_players 
SET result = 'victory'
WHERE game_id = 12345 AND player_id = 1;

UPDATE game_players 
SET result = 'defeat'
WHERE game_id = 12345 AND player_id = 2;

-- 3. 통계 저장
INSERT INTO game_statistics (...) VALUES (...);

-- 4. 누적 통계 업데이트
UPDATE player_rts_stats 
SET total_games = total_games + 1,
    wins = wins + 1,
    win_rate = wins / total_games * 100,
    last_game_time = NOW()
WHERE player_id = 1;
```

### 5.2 리더보드 쿼리
```sql
-- ELO 리더보드 상위 100명
SELECT p.user_id, p.username, s.elo_rating, s.wins, s.losses, s.win_rate
FROM player_rts_stats s
JOIN users p ON s.player_id = p.user_id
ORDER BY s.elo_rating DESC
LIMIT 100;

-- 월별 최고 승률
SELECT player_id, username, wins, losses, (wins/(wins+losses)*100) AS win_rate
FROM player_rts_stats
WHERE MONTH(last_game_time) = MONTH(NOW())
ORDER BY win_rate DESC
LIMIT 50;
```

### 5.3 플레이어 히스토리
```sql
SELECT 
    m.match_date,
    m.result,
    m.game_duration,
    m.opponent_id,
    u.username AS opponent_name,
    m.mmr_change,
    s.map_name
FROM match_history m
LEFT JOIN users u ON m.opponent_id = u.user_id
LEFT JOIN game_sessions s ON m.game_id = s.game_id
WHERE m.player_id = 1
ORDER BY m.match_date DESC
LIMIT 20;
```

### 5.4 Redis 명령 예제
```redis
# 게임 상태 설정
HSET game:12345 map_id 1 game_mode 1v1 status in_progress

# 플레이어 자원 업데이트
HSET game:12345:player:1 resources.minerals 650 resources.gas 150

# 유닛 생성
ZADD game:12345:units 1 '{"unit_id":1, "type":"worker", ...}'

# 명령 추가
RPUSH game:12345:commands:1 '{"command_type":"move", ...}'

# 포그 맵 업데이트
SETBIT game:12345:fog:1 12345 1

# 게임 이벤트 기록
ZADD game:12345:events 1702154401000 '{"event_type":"unit_killed", ...}'
```

---

## 6. 성능 모니터링

### 6.1 모니터링 쿼리
```sql
-- 활성 게임 수
SELECT COUNT(*) FROM game_sessions WHERE status = 'in_progress';

-- 평균 게임 지속 시간
SELECT AVG(duration_seconds) FROM game_sessions WHERE status = 'completed';

-- 평균 플레이어 APM
SELECT AVG(actions_per_minute) FROM game_statistics;

-- 평균 이기는 데 걸리는 시간
SELECT AVG(duration_seconds) 
FROM game_sessions 
WHERE status = 'completed' 
AND winner_player_id IS NOT NULL;
```

### 6.2 용량 계획
```
MySQL:
- 게임 세션: 1000만 게임 × 1KB = 10GB
- 통계: 1000만 게임 × 500B = 5GB
- 총 크기: ~50GB (인덱스 포함)
- 월 증가: ~100MB

Redis:
- 활성 게임: 100게임 × 100KB = 10MB
- 플레이어 세션: 10000명 × 1KB = 10MB
- 레더보드: 10000명 × 100B = 1MB
- 총 메모리: ~100MB
```

