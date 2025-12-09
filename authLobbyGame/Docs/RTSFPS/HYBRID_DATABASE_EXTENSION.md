# FPS/RTS 하이브리드 게임 데이터베이스 확장

## 1. 데이터베이스 구조

```
MySQL Database: hybrid_game_db

테이블 그룹:
├─ 게임 세션 (game_sessions, game_events)
├─ 플레이어 통계 (player_fps_stats, player_rts_stats)
├─ 맵 & 밸런싱 (maps, difficulty_settings)
├─ 성과 & 보상 (achievements, seasonal_rewards)
└─ 분석 데이터 (match_telemetry, bot_performance)
```

---

## 2. 핵심 테이블 정의

### 2.1 game_sessions

```sql
CREATE TABLE game_sessions (
    session_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    started_at TIMESTAMP,
    ended_at TIMESTAMP,
    
    -- 참여자
    fps_player_ids JSON,  -- ["player_001", "player_002", "player_003", "player_004"]
    rts_player_id VARCHAR(50),  -- RTS 커맨더
    
    -- 게임 설정
    map_id INT NOT NULL,
    difficulty VARCHAR(20),  -- Easy, Normal, Hard, Extreme
    bot_count INT,
    game_duration_seconds INT,
    
    -- 결과
    winning_team VARCHAR(20),  -- FPS, RTS, Draw
    fps_team_kills INT DEFAULT 0,
    rts_team_kills INT DEFAULT 0,
    
    -- 추가 데이터
    replay_file_path VARCHAR(255),
    server_region VARCHAR(50),
    server_version VARCHAR(20),
    
    FOREIGN KEY (map_id) REFERENCES maps(map_id),
    KEY idx_fps_players (fps_player_ids(10)),
    KEY idx_rts_player (rts_player_id),
    KEY idx_created_at (created_at)
);
```

**샘플 데이터**:
```
session_id: 1001
created_at: 2024-01-20 14:30:00
fps_player_ids: ["alice", "bob", "charlie", "david"]
rts_player_id: "eve"
map_id: 1
difficulty: Normal
game_duration_seconds: 1200
winning_team: FPS
fps_team_kills: 95
rts_team_kills: 5
```

---

### 2.2 fps_player_stats

```sql
CREATE TABLE fps_player_stats (
    stat_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    session_id BIGINT NOT NULL,
    player_id VARCHAR(50) NOT NULL,
    
    -- 전투 통계
    kills INT,
    deaths INT,
    assists INT,
    headshots INT,
    
    -- 총기 통계
    total_shots_fired INT,
    shots_hit INT,
    accuracy_percent FLOAT,
    most_used_weapon VARCHAR(50),
    
    -- 생존 통계
    time_alive_seconds INT,
    furthest_traveled_meters INT,
    damage_dealt INT,
    damage_taken INT,
    
    -- 능력 사용
    abilities_used INT,
    total_healing_done INT,
    total_revives INT,
    
    -- 평가
    performance_score INT,
    mvp_vote BOOLEAN,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (session_id) REFERENCES game_sessions(session_id),
    UNIQUE KEY unique_session_player (session_id, player_id),
    KEY idx_player_id (player_id),
    KEY idx_accuracy (accuracy_percent)
);
```

**K/D 비율 계산**:
```sql
SELECT 
    player_id,
    ROUND(AVG(kills / GREATEST(deaths, 1)), 2) AS avg_kd_ratio,
    COUNT(*) AS games_played
FROM fps_player_stats
GROUP BY player_id
HAVING games_played > 10
ORDER BY avg_kd_ratio DESC;
```

---

### 2.3 rts_player_stats

```sql
CREATE TABLE rts_player_stats (
    stat_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    session_id BIGINT NOT NULL,
    player_id VARCHAR(50) NOT NULL,
    
    -- 유닛 관리
    total_units_spawned INT,
    units_killed INT,
    units_lost INT,
    average_unit_lifespan_seconds INT,
    
    -- 전술 통계
    commands_issued INT,
    average_command_response_time_ms FLOAT,
    retreat_count INT,
    aggressive_moves INT,
    
    -- 맵 제어
    map_coverage_percent FLOAT,
    peak_unit_count INT,
    resource_efficiency_percent FLOAT,
    
    -- 승리 기여
    fps_team_eliminations_caused INT,
    fps_player_deaths_prevented INT,
    
    -- 평가
    strategic_score INT,
    decision_quality_percent FLOAT,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (session_id) REFERENCES game_sessions(session_id),
    UNIQUE KEY unique_session_player (session_id, player_id),
    KEY idx_player_id (player_id)
);
```

---

### 2.4 bot_performance

```sql
CREATE TABLE bot_performance (
    perf_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    session_id BIGINT NOT NULL,
    bot_id INT NOT NULL,
    bot_difficulty VARCHAR(20),
    
    -- 성과
    kills INT,
    deaths INT,
    shots_fired INT,
    shots_hit INT,
    accuracy_percent FLOAT,
    
    -- 행동 통계
    time_in_combat_seconds INT,
    time_in_cover_seconds INT,
    cover_usage_percent FLOAT,
    
    -- 전술 유효성
    flanking_attempts INT,
    successful_flanks INT,
    retreat_count INT,
    
    -- AI 성능
    decision_latency_ms INT,
    pathfinding_efficiency FLOAT,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (session_id) REFERENCES game_sessions(session_id),
    KEY idx_session_bot (session_id, bot_id),
    KEY idx_accuracy (accuracy_percent)
);
```

---

### 2.5 achievements

```sql
CREATE TABLE achievements (
    achievement_id INT PRIMARY KEY AUTO_INCREMENT,
    
    achievement_code VARCHAR(50) UNIQUE,
    name VARCHAR(100),
    description VARCHAR(255),
    category VARCHAR(50),  -- fps, rts, general
    points INT,
    
    -- 달성 조건
    condition_type VARCHAR(50),  -- kills, accuracy, win_streak, etc.
    condition_value INT,
    
    icon_url VARCHAR(255),
    rarity VARCHAR(20)  -- Common, Rare, Epic, Legendary
);
```

**샘플**:
```sql
INSERT INTO achievements (achievement_code, name, description, category, points, condition_type, condition_value)
VALUES 
('headshot_master', 'Headshot Master', '10 headshots in one game', 'fps', 100, 'headshots', 10),
('accuracy_100', 'Sharpshooter', '90% accuracy with 30+ shots', 'fps', 150, 'accuracy', 90),
('win_streak_5', 'On Fire', 'Win 5 consecutive games', 'general', 200, 'win_streak', 5),
('unit_commander', 'Unit Commander', 'Eliminate 100 FPS players via units', 'rts', 150, 'fps_kills', 100);
```

---

## 3. Redis 캐시 구조

### 3.1 게임 중 활성 상태

```
Key Pattern: game:session:{session_id}

hash game:session:1001 {
    fps_team_kills: "50"
    rts_team_kills: "10"
    time_remaining: "600"
    fps_player_1: "{pos_x:100,pos_y:50,health:85}"
    fps_player_2: "{pos_x:105,pos_y:52,health:92}"
    ...
    bot_1: "{pos_x:200,pos_y:100,state:combat,health:75}"
    ...
}

TTL: GAME_DURATION + 300s (게임 종료 후 5분)
```

### 3.2 플레이어 세션

```
Key: player:session:{player_id}

hash player:session:alice {
    current_session: "1001"
    last_activity: "2024-01-20T14:35:00Z"
    position: "{x:100,y:50,z:200}"
    health: "85"
    inventory: "{weapon:rifle,ammo:240,grenades:2}"
}

TTL: 1시간
```

### 3.3 통계 캐싱

```
Key: stats:daily:{date}
Key: stats:weekly:{week}
Key: stats:monthly:{month}

hash stats:daily:2024-01-20 {
    total_games: "150"
    avg_fps_win_rate: "0.48"
    avg_game_duration: "1200"
    most_used_weapon: "assault_rifle"
    total_players: "450"
}

TTL: 다음 기간 시작까지
```

---

## 4. 데이터 쿼리 예제

### 4.1 플레이어 성과 조회

```sql
SELECT 
    fps.player_id,
    COUNT(DISTINCT fps.session_id) AS games_played,
    ROUND(SUM(fps.kills) / COUNT(*), 2) AS avg_kills,
    ROUND(SUM(fps.deaths) / COUNT(*), 2) AS avg_deaths,
    ROUND(SUM(fps.kills) / GREATEST(SUM(fps.deaths), 1), 2) AS kd_ratio,
    ROUND(AVG(fps.accuracy_percent), 1) AS avg_accuracy,
    ROUND(COUNT(CASE WHEN gs.winning_team = 'FPS' THEN 1 END) * 100.0 / COUNT(*), 1) AS win_rate_percent
FROM fps_player_stats fps
JOIN game_sessions gs ON fps.session_id = gs.session_id
WHERE fps.created_at >= DATE_SUB(NOW(), INTERVAL 7 DAY)
GROUP BY fps.player_id
HAVING games_played >= 5
ORDER BY win_rate_percent DESC
LIMIT 10;
```

### 4.2 맵별 밸런스 분석

```sql
SELECT 
    m.map_name,
    COUNT(*) AS total_games,
    ROUND(COUNT(CASE WHEN gs.winning_team = 'FPS' THEN 1 END) * 100.0 / COUNT(*), 1) AS fps_win_rate_percent,
    AVG(gs.game_duration_seconds) / 60 AS avg_duration_minutes,
    ROUND(AVG(gs.fps_team_kills), 1) AS avg_fps_kills,
    ROUND(AVG(gs.rts_team_kills), 1) AS avg_rts_kills
FROM game_sessions gs
JOIN maps m ON gs.map_id = m.map_id
WHERE gs.created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
GROUP BY gs.map_id, m.map_name
ORDER BY total_games DESC;
```

### 4.3 난이도별 승률

```sql
SELECT 
    gs.difficulty,
    COUNT(*) AS total_games,
    COUNT(CASE WHEN gs.winning_team = 'FPS' THEN 1 END) AS fps_wins,
    ROUND(COUNT(CASE WHEN gs.winning_team = 'FPS' THEN 1 END) * 100.0 / COUNT(*), 1) AS fps_win_rate_percent,
    AVG(gs.game_duration_seconds) / 60 AS avg_duration_minutes,
    COUNT(DISTINCT CONCAT_WS(',', JSON_EXTRACT(gs.fps_player_ids, '$[0]'))) AS unique_fps_players
FROM game_sessions gs
WHERE gs.created_at >= DATE_SUB(NOW(), INTERVAL 7 DAY)
GROUP BY gs.difficulty
ORDER BY FIELD(gs.difficulty, 'Easy', 'Normal', 'Hard', 'Extreme');
```

---

## 5. 데이터 마이그레이션

### 5.1 초기화 스크립트

```sql
-- 1. 모든 테이블 생성
SOURCE hybrid_game_schema.sql;

-- 2. 기본 데이터 삽입
INSERT INTO maps (map_name, map_version, size_x, size_y, fps_spawn_x, fps_spawn_y)
VALUES 
('Industrial Zone', '1.0', 300, 300, 150, 150),
('Mountain Pass', '1.0', 400, 400, 200, 200),
('Urban District', '1.1', 350, 350, 175, 175);

-- 3. 초기 인덱스 생성
ALTER TABLE game_sessions ADD INDEX idx_map_difficulty (map_id, difficulty);
ALTER TABLE fps_player_stats ADD INDEX idx_weapon_accuracy (most_used_weapon, accuracy_percent);

-- 4. 통계 초기화
INSERT INTO player_statistics (player_id, total_games, wins, losses)
SELECT DISTINCT player_id, 0, 0, 0 FROM fps_player_stats;
```

### 5.2 데이터 정제 작업

```sql
-- 이상한 데이터 식별
SELECT 
    session_id,
    MAX(accuracy_percent) AS max_accuracy
FROM fps_player_stats
GROUP BY session_id
HAVING max_accuracy > 100  -- 불가능한 값
OR MAX(accuracy_percent) < 0;

-- 결함 있는 기록 제거
DELETE FROM fps_player_stats
WHERE accuracy_percent < 0 OR accuracy_percent > 100
OR shots_fired < shots_hit;  -- 논리적 오류
```

---

## 6. 성능 최적화

### 6.1 파티셔닝 전략

```sql
-- 월별 파티셔닝 (game_sessions)
ALTER TABLE game_sessions
PARTITION BY RANGE (YEAR(created_at)*100 + MONTH(created_at)) (
    PARTITION p202312 VALUES LESS THAN (202401),
    PARTITION p202401 VALUES LESS THAN (202402),
    PARTITION p202402 VALUES LESS THAN (202403),
    ...
);
```

### 6.2 쿼리 최적화

```sql
-- 느린 쿼리 로깅 활성화
SET GLOBAL slow_query_log = 'ON';
SET GLOBAL long_query_time = 1;  -- 1초 이상 쿼리 기록

-- 실행 계획 분석
EXPLAIN FORMAT=JSON
SELECT * FROM fps_player_stats 
WHERE accuracy_percent > 80 
AND created_at >= DATE_SUB(NOW(), INTERVAL 7 DAY);
```

### 6.3 인덱스 전략

```
자주 조회하는 필드별 인덱스:
• session_id (조인용)
• player_id (플레이어 검색)
• created_at (날짜 범위 쿼리)
• accuracy_percent (순위 쿼리)
• winning_team (결과 필터링)

복합 인덱스:
• (session_id, player_id)
• (player_id, created_at)
• (map_id, difficulty, winning_team)
```

---

## 7. 데이터 백업 전략

### 7.1 자동 백업

```bash
# 일일 백업 (자정)
0 0 * * * /usr/bin/mysqldump -u hybrid_user -p${MYSQL_PASSWORD} hybrid_game_db | gzip > /backups/hybrid_db_$(date +\%Y\%m\%d).sql.gz

# 주간 아카이브 (일요일)
0 1 * * 0 aws s3 cp /backups/hybrid_db_*.sql.gz s3://game-backups/hybrid_game_db/ --storage-class GLACIER
```

### 7.2 복구 절차

```sql
-- 백업에서 복구
gunzip < /backups/hybrid_db_20240120.sql.gz | mysql -u hybrid_user -p${MYSQL_PASSWORD} hybrid_game_db

-- 특정 시점으로 복구 (바이너리 로그 기반)
mysqlbinlog --stop-datetime="2024-01-20 10:00:00" /var/log/mysql/mysql-bin.* | mysql -u hybrid_user -p${MYSQL_PASSWORD}
```

---

## 8. 모니터링 대시보드 쿼리

### 8.1 실시간 활성 게임

```sql
SELECT 
    gs.session_id,
    m.map_name,
    gs.difficulty,
    TIME_TO_SEC(TIMEDIFF(NOW(), gs.started_at)) / 60 AS duration_minutes,
    gs.fps_team_kills,
    gs.rts_team_kills,
    gs.bot_count,
    COUNT(DISTINCT fps.player_id) AS active_fps_players
FROM game_sessions gs
JOIN maps m ON gs.map_id = m.map_id
LEFT JOIN fps_player_stats fps ON gs.session_id = fps.session_id
WHERE gs.ended_at IS NULL
GROUP BY gs.session_id, m.map_name, gs.difficulty
ORDER BY gs.started_at DESC;
```

### 8.2 성능 지표

```sql
-- 서버 부하 (시간당 게임 수)
SELECT 
    DATE_FORMAT(created_at, '%Y-%m-%d %H:00') AS hour,
    COUNT(*) AS games_started,
    AVG(game_duration_seconds) / 60 AS avg_duration_minutes,
    ROUND(AVG(fps_team_kills + rts_team_kills), 1) AS avg_total_kills
FROM game_sessions
GROUP BY DATE_FORMAT(created_at, '%Y-%m-%d %H:00')
ORDER BY hour DESC
LIMIT 24;
```

---

## 9. 데이터 보존 정책

```
데이터 보관 기간:

• 게임 세션: 2년 (분석 목적)
• 플레이어 통계: 2년 (순위 유지)
• 봇 성능: 1년 (AI 학습용)
• 리플레이 파일: 6개월 (스토리지 절감)
• 이벤트 로그: 90일 (트러블슈팅용)

보관 후 삭제:
→ 개인정보 보호 규정 준수
→ 스토리지 비용 절감
→ 쿼리 성능 유지
```

---

## 10. 데이터베이스 체크리스트

```
설정:
  ✓ MySQL 8.0+ 설치
  ✓ hybrid_game_db 생성
  ✓ 사용자 권한 설정 (select, insert, update, delete)
  ✓ 자동 백업 설정

테이블:
  ✓ game_sessions 생성
  ✓ fps_player_stats 생성
  ✓ rts_player_stats 생성
  ✓ bot_performance 생성
  ✓ achievements 생성
  ✓ maps 생성

인덱스:
  ✓ 주요 조인 인덱스 생성
  ✓ 검색 필드 인덱스 생성
  ✓ 복합 인덱스 생성

모니터링:
  ✓ 쿼리 로깅 활성화
  ✓ 느린 쿼리 모니터링
  ✓ 디스크 사용량 모니터링
  ✓ 연결 풀 설정 (20-50 연결)
```

