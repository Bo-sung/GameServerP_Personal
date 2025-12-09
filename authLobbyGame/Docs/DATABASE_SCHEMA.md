# 데이터베이스 스키마

## 데이터베이스 아키텍처 개요

### 역할 분담

#### MySQL (주 데이터베이스)
- **목적**: 영구 데이터 저장
- **버전**: MySQL 8.0 이상
- **문자셋**: utf8mb4
- **엔진**: InnoDB
- **용도**:
  - 사용자 계정 정보
  - 게임 플레이 기록 (히스토리)
  - 통계 데이터 (집계된 데이터)
  - 게임 서버 메타데이터
  - 토큰 리프레시 정보

#### Redis (캐싱 및 세션 DB)
- **목적**: 실시간 데이터 캐싱, 세션 관리, 휘발성 데이터
- **버전**: Redis 7.0 이상
- **지속성**: RDB + AOF
- **용도**:
  - 활성 세션 (로그인 상태)
  - 실시간 온라인 유저 목록
  - 방 상태 (임시 데이터)
  - 게임 서버 실시간 상태 (하트비트)
  - 매칭 큐
  - 실시간 통계 (집계 전)
  - 리더보드 (캐싱)

### 데이터 동기화 전략
- **MySQL → Redis**: 애플리케이션 시작 시 리더보드, 서버 목록 등 캐싱
- **Redis → MySQL**: 게임 종료 시 결과 저장, 주기적 통계 집계
- **TTL 정책**: Redis 데이터는 TTL로 자동 만료, 중요 데이터는 MySQL에 영구 저장

---

## MySQL 스키마

### 1. Users 테이블
```sql
CREATE TABLE users (
    user_id VARCHAR(36) PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    salt VARCHAR(64) NOT NULL,

    -- 프로필 정보
    nickname VARCHAR(50),
    level INT DEFAULT 1,
    experience BIGINT DEFAULT 0,

    -- 통계
    total_games INT DEFAULT 0,
    wins INT DEFAULT 0,
    losses INT DEFAULT 0,

    -- 시간 정보
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_login DATETIME,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    -- 계정 상태
    status ENUM('active', 'banned', 'deleted') DEFAULT 'active',

    INDEX idx_username (username),
    INDEX idx_email (email),
    INDEX idx_status (status),
    INDEX idx_last_login (last_login)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 2. GameRecords 테이블
```sql
CREATE TABLE game_records (
    record_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    session_id VARCHAR(36) NOT NULL,

    -- 게임 정보 (범용)
    game_mode VARCHAR(50) NOT NULL COMMENT '게임 모드 (애플리케이션 정의)',
    result ENUM('win', 'lose', 'draw') NOT NULL,
    score INT DEFAULT 0 COMMENT '점수 또는 순위',

    -- 상세 통계 (JSON - 게임별 커스텀 데이터)
    stats JSON COMMENT '게임 특화 통계 (kills, deaths, items 등)',

    -- 시간 정보
    started_at DATETIME NOT NULL,
    ended_at DATETIME NOT NULL,
    duration_seconds INT NOT NULL,

    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_session_id (session_id),
    INDEX idx_started_at (started_at),
    INDEX idx_game_mode (game_mode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- JSON stats 예시:
-- FPS 게임: {"kills": 15, "deaths": 8, "headshots": 5}
-- 퍼즐 게임: {"moves": 120, "time": 180, "hints_used": 2}
-- MOBA: {"kills": 10, "deaths": 3, "assists": 8, "gold_earned": 15000}
```

### 3. GameSessions 테이블
```sql
CREATE TABLE game_sessions (
    session_id VARCHAR(36) PRIMARY KEY,
    room_id VARCHAR(36) NOT NULL,
    game_server_id VARCHAR(36) NOT NULL,

    -- 세션 정보
    session_token VARCHAR(255) NOT NULL,
    max_players INT NOT NULL,
    current_players INT DEFAULT 0,

    -- 시간 정보
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    started_at DATETIME,
    ended_at DATETIME,
    expires_at DATETIME NOT NULL,

    -- 상태
    status ENUM('waiting', 'active', 'finished', 'expired') DEFAULT 'waiting',

    INDEX idx_room_id (room_id),
    INDEX idx_game_server_id (game_server_id),
    INDEX idx_status (status),
    INDEX idx_expires_at (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 4. GameServers 테이블
```sql
CREATE TABLE game_servers (
    server_id VARCHAR(36) PRIMARY KEY,
    server_name VARCHAR(100) NOT NULL,

    -- 네트워크 정보
    ip_address VARCHAR(45) NOT NULL,
    tcp_port INT NOT NULL,
    udp_port INT NOT NULL,

    -- 용량 정보
    max_capacity INT DEFAULT 100,
    current_load INT DEFAULT 0,

    -- 상태
    status ENUM('online', 'offline', 'maintenance') DEFAULT 'offline',
    region VARCHAR(50),

    -- 시간 정보
    registered_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_heartbeat DATETIME,

    INDEX idx_status (status),
    INDEX idx_region (region),
    INDEX idx_current_load (current_load)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 5. UserStats 테이블
```sql
CREATE TABLE user_stats (
    user_id VARCHAR(36) PRIMARY KEY,

    -- 전투 통계
    total_kills INT DEFAULT 0,
    total_deaths INT DEFAULT 0,
    total_damage BIGINT DEFAULT 0,

    -- 시간 통계
    total_playtime_seconds BIGINT DEFAULT 0,

    -- 랭킹 정보
    mmr INT DEFAULT 1000,
    rank_tier VARCHAR(20) DEFAULT 'Bronze',

    -- 업적
    achievements JSON,

    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 6. RefreshTokens 테이블
```sql
CREATE TABLE refresh_tokens (
    token_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,

    token_hash VARCHAR(255) NOT NULL UNIQUE,

    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    expires_at DATETIME NOT NULL,
    revoked BOOLEAN DEFAULT FALSE,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_token_hash (token_hash),
    INDEX idx_expires_at (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

---

## Redis 데이터 구조

### 1. 활성 세션 (Active Sessions)
```
키 패턴: session:{session_id}
타입: Hash
TTL: 3600 (1시간)

필드:
- user_id: string
- username: string
- token: string
- created_at: timestamp
- expires_at: timestamp
- server_type: "lobby" | "game"
- server_id: string

예시:
HSET session:550e8400-e29b-41d4-a716-446655440000 user_id "user123"
HSET session:550e8400-e29b-41d4-a716-446655440000 username "player1"
HSET session:550e8400-e29b-41d4-a716-446655440000 token "jwt_token_here"
EXPIRE session:550e8400-e29b-41d4-a716-446655440000 3600
```

### 2. 온라인 유저 목록 (Online Users)
```
키 패턴: online_users
타입: Sorted Set
Score: last_activity_timestamp

명령어:
ZADD online_users 1670000000 "user123"
ZREMRANGEBYSCORE online_users 0 (현재시간 - 5분)  # 비활성 유저 제거
ZCARD online_users  # 온라인 유저 수
```

### 3. 방 정보 (Room State)
```
키 패턴: room:{room_id}
타입: Hash
TTL: 7200 (2시간)

필드:
- room_id: string
- room_name: string
- host_user_id: string
- max_players: int
- current_players: int
- game_server_id: string
- status: "waiting" | "full" | "playing"
- is_private: boolean
- created_at: timestamp
- last_activity: timestamp  # 마지막 활동 시간 (비활성 방 제거용)

예시:
HMSET room:room123 room_name "MyRoom" host_user_id "user123" max_players 4 current_players 2 \
    last_activity 1670000000
EXPIRE room:room123 7200

비활성 방 자동 제거:
- 로비 서버에서 주기적으로 실행 (5분마다 권장)
- status가 "waiting"이고 last_activity가 30분 이상 지난 방 삭제
- TTL은 최종 안전장치 (2시간)
```

### 4. 방 참가자 목록 (Room Players)
```
키 패턴: room:{room_id}:players
타입: Set
TTL: 7200 (2시간)

명령어:
SADD room:room123:players "user123" "user456"
SCARD room:room123:players  # 현재 플레이어 수
SMEMBERS room:room123:players  # 모든 플레이어 목록
```

### 5. 게임 서버 상태 (Game Server Status)
```
키 패턴: gameserver:{server_id}
타입: Hash
TTL: 60 (1분, 하트비트로 갱신)

필드:
- server_id: string
- ip_address: string
- tcp_port: int
- udp_port: int
- current_load: int
- max_capacity: int
- status: "online" | "offline" | "maintenance"
- last_heartbeat: timestamp

예시:
HMSET gameserver:gs1 ip_address "192.168.1.100" tcp_port 8888 current_load 30 max_capacity 100
EXPIRE gameserver:gs1 60
```

### 6. 게임 서버 목록 (Available Game Servers)
```
키 패턴: gameservers:available
타입: Sorted Set
Score: current_load (낮을수록 우선)

명령어:
ZADD gameservers:available 30 "gs1"  # 로드 30%
ZADD gameservers:available 60 "gs2"  # 로드 60%
ZRANGE gameservers:available 0 0  # 가장 로드가 낮은 서버
```

### 7. 매칭 큐 (Matchmaking Queue)
```
키 패턴: matchmaking:{game_mode}
타입: Sorted Set
Score: timestamp (먼저 들어온 순서)

명령어:
ZADD matchmaking:mode1 1670000000 "user123"
ZRANGE matchmaking:mode1 0 3  # 4명 매칭
ZREM matchmaking:mode1 "user123"  # 큐에서 제거

참고: game_mode는 애플리케이션에서 정의 (예: "standard", "ranked", "custom" 등)
```

### 8. 유저 위치 정보 (User Location)
```
키 패턴: user:{user_id}:location
타입: String
TTL: 300 (5분)

값: "lobby" | "game:{game_server_id}"

명령어:
SET user:user123:location "lobby" EX 300
SET user:user123:location "game:gs1" EX 300
```

### 9. 토큰 블랙리스트 (Token Blacklist)
```
키 패턴: blacklist:token:{token_hash}
타입: String
TTL: 토큰 남은 유효 시간

명령어:
SET blacklist:token:abc123hash "revoked" EX 3600
EXISTS blacklist:token:abc123hash
```

### 10. 실시간 통계 (Real-time Stats)
```
키 패턴: stats:realtime
타입: Hash

필드:
- online_users: int
- active_games: int
- total_players_in_game: int
- lobby_users: int

명령어:
HINCRBY stats:realtime online_users 1
HGET stats:realtime online_users
```

### 11. 채팅 메시지 캐시 (Chat Messages)
```
키 패턴: chat:{room_id}
타입: List (최근 100개만 유지)
TTL: 3600 (1시간)

명령어:
LPUSH chat:room123 '{"user":"player1","msg":"Hello","time":1670000000}'
LTRIM chat:room123 0 99  # 최근 100개만 유지
LRANGE chat:room123 0 -1  # 모든 메시지 조회
```

### 12. 리더보드 (Leaderboard)
```
키 패턴: leaderboard:{type}
타입: Sorted Set
Score: 점수

명령어:
ZADD leaderboard:mmr 2500 "user123"
ZREVRANGE leaderboard:mmr 0 9 WITHSCORES  # 상위 10명
ZRANK leaderboard:mmr "user123"  # 순위 조회
```

---

## 데이터 흐름

### 1. 로그인 시
```
1. 클라이언트 → 인증 서버: 로그인 요청
2. 인증 서버 → MySQL: 사용자 조회 및 비밀번호 검증
3. 인증 서버 → MySQL: last_login 업데이트
4. 인증 서버 → Redis: 세션 생성 (session:{session_id})
5. 인증 서버 → Redis: 온라인 유저 추가 (ZADD online_users)
6. 인증 서버 → 클라이언트: JWT 토큰 반환
```

### 2. 로비 진입 시
```
1. 클라이언트 → 로비 서버: 토큰과 함께 연결
2. 로비 서버 → Redis: 세션 검증 (HGETALL session:{session_id})
3. 로비 서버 → Redis: 유저 위치 설정 (SET user:{user_id}:location "lobby")
4. 로비 서버 → Redis: 온라인 유저 활동 갱신 (ZADD online_users)
5. 로비 서버 → Redis: 방 목록 조회 (SCAN 0 MATCH room:* COUNT 100)
6. 로비 서버 → 클라이언트: 방 목록 전송
```

### 3. 방 생성/참가 시
```
1. 클라이언트 → 로비 서버: 방 생성/참가 요청
2. 로비 서버 → Redis: 방 정보 생성 (HMSET room:{room_id})
3. 로비 서버 → Redis: 참가자 추가 (SADD room:{room_id}:players)
4. 로비 서버 → Redis: 게임 서버 선택 (ZRANGE gameservers:available)
5. 로비 서버 → MySQL: 게임 세션 기록 (INSERT game_sessions)
6. 로비 서버 → 클라이언트: 게임 서버 정보 전송
```

### 4. 게임 플레이 중
```
1. 클라이언트 → 게임 서버: 게임 연결 (세션 토큰 포함)
2. 게임 서버 → Redis: 세션 검증 (HGETALL session:{session_id})
3. 게임 서버 → Redis: 유저 위치 업데이트 (SET user:{user_id}:location "game:gs1")
4. 게임 서버 → Redis: 실시간 통계 업데이트 (HINCRBY stats:realtime active_games 1)
5. 게임 서버 → Redis: 게임 서버 부하 업데이트 (HSET gameserver:gs1 current_load XX)
6. 게임 진행 (메모리 내에서 실시간 처리)
7. 게임 종료 → Redis: 실시간 통계 감소 (HINCRBY stats:realtime active_games -1)
8. 게임 종료 → MySQL: 게임 기록 저장 (INSERT INTO game_records)
9. 게임 종료 → MySQL: 유저 통계 업데이트 (UPDATE user_stats)
10. 게임 종료 → Redis: 방 정보 삭제 (DEL room:{room_id})
```

### 5. 로그아웃 시
```
1. 클라이언트 → 서버: 로그아웃 요청
2. 서버 → Redis: 세션 삭제 (DEL session:{session_id})
3. 서버 → Redis: 온라인 유저 제거 (ZREM online_users)
4. 서버 → Redis: 유저 위치 제거 (DEL user:{user_id}:location)
5. 서버 → Redis: 토큰 블랙리스트 추가 (SET blacklist:token:{token_hash})
```

---

## Redis 성능 최적화

### 1. 키 만료 정책
```
- 세션: 1시간 (EXPIRE 3600)
- 방 정보: 2시간 (EXPIRE 7200)
- 게임 서버 상태: 1분 (하트비트로 갱신)
- 유저 위치: 5분 (EXPIRE 300)
- 채팅 메시지: 1시간 (EXPIRE 3600)
```

### 2. 메모리 관리
```
maxmemory 2gb
maxmemory-policy allkeys-lru
```

### 3. 지속성 설정
```
# RDB 스냅샷
save 900 1      # 15분마다 1개 이상 키 변경시
save 300 10     # 5분마다 10개 이상 키 변경시
save 60 10000   # 1분마다 10000개 이상 키 변경시

# AOF
appendonly yes
appendfsync everysec
```

### 4. 파이프라인 사용
```
# 여러 명령을 한 번에 전송
PIPELINE
SET key1 value1
SET key2 value2
SET key3 value3
EXEC
```

---

## MySQL 성능 최적화

### 1. 인덱스 전략
- 자주 검색되는 컬럼에 인덱스 추가
- 복합 인덱스 활용 (WHERE절에 자주 함께 사용되는 컬럼)
- 커버링 인덱스로 SELECT 성능 향상

### 2. 파티셔닝
```sql
-- game_records 테이블 월별 파티셔닝
ALTER TABLE game_records
PARTITION BY RANGE (YEAR(started_at) * 100 + MONTH(started_at)) (
    PARTITION p202501 VALUES LESS THAN (202502),
    PARTITION p202502 VALUES LESS THAN (202503),
    -- ...
);
```

### 3. 쿼리 최적화
```sql
-- 인덱스 힌트 사용
SELECT * FROM users USE INDEX (idx_username) WHERE username = 'player1';

-- EXPLAIN으로 쿼리 분석
EXPLAIN SELECT * FROM game_records WHERE user_id = 'user123';
```

### 4. 커넥션 풀링
```
최소 커넥션: 10
최대 커넥션: 50
커넥션 타임아웃: 30초
```

---

## 백업 전략

### MySQL 백업
```bash
# 일일 전체 백업 (새벽 2시)
mysqldump -u root -p game_server_db > backup_$(date +%Y%m%d).sql

# 주간 전체 백업 유지
# 월간 아카이브 백업
```

### Redis 백업
```bash
# RDB 파일 자동 저장 (redis.conf 설정)
# 중요 데이터는 MySQL에도 동기화
```

---

## 모니터링 쿼리

### MySQL 모니터링
```sql
-- 활성 사용자 수
SELECT COUNT(*) FROM users WHERE status = 'active';

-- 오늘 게임 수
SELECT COUNT(*) FROM game_records WHERE DATE(started_at) = CURDATE();

-- 서버별 부하
SELECT server_id, current_load, max_capacity
FROM game_servers
WHERE status = 'online'
ORDER BY current_load;
```

### Redis 모니터링
```bash
# 메모리 사용량
INFO memory

# 온라인 유저 수
ZCARD online_users

# 활성 방 개수
KEYS room:* | wc -l

# 서버 통계
HGETALL stats:realtime
```
