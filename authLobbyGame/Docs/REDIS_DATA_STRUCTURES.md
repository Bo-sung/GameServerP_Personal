# Redis 데이터 구조 상세 설명

## 개요
이 문서는 게임 서버에서 사용하는 Redis 데이터 구조를 상세히 설명합니다.

---

## 1. 활성 세션 (Active Sessions)

### 키 패턴
```
session:{session_id}
```

### 데이터 타입
Hash

### TTL
3600초 (1시간)

### 필드
```redis
user_id: VARCHAR(36)
username: VARCHAR(50)
token: VARCHAR(255)
created_at: UNIX_TIMESTAMP
expires_at: UNIX_TIMESTAMP
server_type: ENUM("lobby", "game")
server_id: VARCHAR(36)
last_activity: UNIX_TIMESTAMP
```

### 예시 명령어
```redis
# 세션 생성
HMSET session:550e8400-e29b-41d4-a716-446655440000 \
    user_id "user123" \
    username "player1" \
    token "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
    created_at 1670000000 \
    expires_at 1670003600 \
    server_type "lobby" \
    server_id "lobby-1" \
    last_activity 1670000000

EXPIRE session:550e8400-e29b-41d4-a716-446655440000 3600

# 세션 조회
HGETALL session:550e8400-e29b-41d4-a716-446655440000

# 세션 존재 확인
EXISTS session:550e8400-e29b-41d4-a716-446655440000

# 세션 삭제
DEL session:550e8400-e29b-41d4-a716-446655440000
```

### C# 사용 예시
```csharp
var redis = ConnectionMultiplexer.Connect("localhost");
var db = redis.GetDatabase();

// 세션 생성
var sessionId = Guid.NewGuid().ToString();
var key = $"session:{sessionId}";

db.HashSet(key, new HashEntry[] {
    new HashEntry("user_id", userId),
    new HashEntry("username", username),
    new HashEntry("token", token),
    new HashEntry("created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    new HashEntry("expires_at", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()),
    new HashEntry("server_type", "lobby"),
    new HashEntry("server_id", serverId)
});

db.KeyExpire(key, TimeSpan.FromHours(1));

// 세션 조회
var session = db.HashGetAll(key);
```

---

## 2. 온라인 유저 목록 (Online Users)

### 키 패턴
```
online_users
```

### 데이터 타입
Sorted Set

### Score
마지막 활동 시간 (UNIX Timestamp)

### 예시 명령어
```redis
# 유저 추가/업데이트
ZADD online_users 1670000000 "user123"

# 비활성 유저 제거 (5분 이상 활동 없음)
ZREMRANGEBYSCORE online_users 0 (현재시간 - 300)

# 온라인 유저 수
ZCARD online_users

# 온라인 유저 목록 (최근 활동 순)
ZREVRANGE online_users 0 -1

# 특정 유저 온라인 확인
ZSCORE online_users "user123"

# 유저 제거
ZREM online_users "user123"
```

### C# 사용 예시
```csharp
// 유저 온라인 상태로 설정
db.SortedSetAdd("online_users", userId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

// 비활성 유저 제거
var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
db.SortedSetRemoveRangeByScore("online_users", 0, cutoffTime);

// 온라인 유저 수
var onlineCount = db.SortedSetLength("online_users");
```

---

## 3. 방 정보 (Room State)

### 키 패턴
```
room:{room_id}
```

### 데이터 타입
Hash

### TTL
7200초 (2시간)

### 필드
```redis
room_id: VARCHAR(36)
room_name: VARCHAR(100)
host_user_id: VARCHAR(36)
max_players: INT
current_players: INT
game_server_id: VARCHAR(36)
status: ENUM("waiting", "full", "playing", "finished")
is_private: BOOL
password_hash: VARCHAR(255) (optional)
created_at: UNIX_TIMESTAMP
```

### 예시 명령어
```redis
# 방 생성
HMSET room:room123 \
    room_id "room123" \
    room_name "MyAwesomeRoom" \
    host_user_id "user123" \
    max_players 4 \
    current_players 1 \
    game_server_id "gs1" \
    status "waiting" \
    is_private "false" \
    created_at 1670000000

EXPIRE room:room123 7200

# 방 정보 조회
HGETALL room:room123

# 현재 플레이어 수 증가
HINCRBY room:room123 current_players 1

# 방 상태 변경
HSET room:room123 status "full"

# 모든 방 목록 조회 (키 스캔)
SCAN 0 MATCH room:* COUNT 100
```

### C# 사용 예시
```csharp
// 방 생성
var roomId = Guid.NewGuid().ToString();
var key = $"room:{roomId}";

db.HashSet(key, new HashEntry[] {
    new HashEntry("room_id", roomId),
    new HashEntry("room_name", roomName),
    new HashEntry("host_user_id", hostUserId),
    new HashEntry("max_players", maxPlayers),
    new HashEntry("current_players", 1),
    new HashEntry("game_server_id", gameServerId),
    new HashEntry("status", "waiting"),
    new HashEntry("is_private", isPrivate),
    new HashEntry("created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    new HashEntry("last_activity", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
});

db.KeyExpire(key, TimeSpan.FromHours(2));

// 플레이어 추가 (활동 시간 갱신)
db.HashIncrement(key, "current_players");
db.HashSet(key, "last_activity", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
```

### 비활성 방 자동 제거 시스템
```csharp
// 로비 서버에서 주기적으로 실행 (예: 5분마다)
public async Task CleanupInactiveRooms()
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var inactiveThreshold = 1800; // 30분

    var cursor = 0L;
    do
    {
        var result = await db.ExecuteAsync("SCAN", cursor, "MATCH", "room:*", "COUNT", "100");
        cursor = (long)result[0];
        var keys = (string[])result[1];

        foreach (var key in keys)
        {
            // 플레이어 목록 키는 건너뛰기
            if (key.EndsWith(":players")) continue;

            var lastActivity = await db.HashGetAsync(key, "last_activity");
            var status = await db.HashGetAsync(key, "status");

            // 비활성 조건 체크
            if (!lastActivity.IsNullOrEmpty)
            {
                var lastActivityTime = (long)lastActivity;
                var isInactive = (now - lastActivityTime) > inactiveThreshold;
                var isWaiting = status == "waiting";

                // 대기 중이고 30분 동안 활동 없으면 제거
                if (isWaiting && isInactive)
                {
                    await DeleteRoom(key);
                    Console.WriteLine($"Removed inactive room: {key}");
                }
            }
        }
    } while (cursor != 0);
}

// 방 완전 삭제
private async Task DeleteRoom(string roomKey)
{
    var roomId = roomKey.Replace("room:", "");

    // 방 정보 삭제
    await db.KeyDeleteAsync(roomKey);

    // 참가자 목록 삭제
    await db.KeyDeleteAsync($"room:{roomId}:players");
}
```

---

## 4. 방 참가자 목록 (Room Players)

### 키 패턴
```
room:{room_id}:players
```

### 데이터 타입
Set

### TTL
7200초 (2시간)

### 예시 명령어
```redis
# 플레이어 추가
SADD room:room123:players "user123" "user456"

# 플레이어 제거
SREM room:room123:players "user123"

# 현재 플레이어 수
SCARD room:room123:players

# 모든 플레이어 목록
SMEMBERS room:room123:players

# 특정 유저 존재 확인
SISMEMBER room:room123:players "user123"
```

### C# 사용 예시
```csharp
var key = $"room:{roomId}:players";

// 플레이어 추가
db.SetAdd(key, userId);

// 플레이어 수 조회
var playerCount = db.SetLength(key);

// 모든 플레이어 조회
var players = db.SetMembers(key);
```

---

## 5. 게임 서버 상태 (Game Server Status)

### 키 패턴
```
gameserver:{server_id}
```

### 데이터 타입
Hash

### TTL
60초 (1분, 하트비트로 갱신)

### 필드
```redis
server_id: VARCHAR(36)
server_name: VARCHAR(100)
ip_address: VARCHAR(45)
tcp_port: INT
udp_port: INT
current_load: INT (0-100 %)
max_capacity: INT
status: ENUM("online", "offline", "maintenance")
region: VARCHAR(50)
last_heartbeat: UNIX_TIMESTAMP
```

### 예시 명령어
```redis
# 게임 서버 등록/갱신
HMSET gameserver:gs1 \
    server_id "gs1" \
    server_name "GameServer-01" \
    ip_address "192.168.1.100" \
    tcp_port 8888 \
    udp_port 8889 \
    current_load 30 \
    max_capacity 100 \
    status "online" \
    region "us-west" \
    last_heartbeat 1670000000

EXPIRE gameserver:gs1 60

# 하트비트 갱신
HSET gameserver:gs1 last_heartbeat 1670000060
EXPIRE gameserver:gs1 60

# 부하 업데이트
HSET gameserver:gs1 current_load 45
```

### C# 사용 예시
```csharp
// 하트비트 전송
var key = $"gameserver:{serverId}";

db.HashSet(key, new HashEntry[] {
    new HashEntry("server_id", serverId),
    new HashEntry("ip_address", ipAddress),
    new HashEntry("tcp_port", tcpPort),
    new HashEntry("udp_port", udpPort),
    new HashEntry("current_load", currentLoad),
    new HashEntry("max_capacity", maxCapacity),
    new HashEntry("status", "online"),
    new HashEntry("last_heartbeat", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
});

db.KeyExpire(key, TimeSpan.FromSeconds(60));
```

---

## 6. 게임 서버 목록 (Available Game Servers)

### 키 패턴
```
gameservers:available
```

### 데이터 타입
Sorted Set

### Score
현재 부하 (낮을수록 우선)

### 예시 명령어
```redis
# 서버 추가/업데이트
ZADD gameservers:available 30 "gs1"
ZADD gameservers:available 60 "gs2"
ZADD gameservers:available 20 "gs3"

# 가장 부하가 낮은 서버 조회
ZRANGE gameservers:available 0 0

# 부하 50% 이하 서버들
ZRANGEBYSCORE gameservers:available 0 50

# 서버 제거
ZREM gameservers:available "gs1"
```

### C# 사용 예시
```csharp
// 서버 부하 업데이트
db.SortedSetAdd("gameservers:available", serverId, currentLoad);

// 가장 여유있는 서버 선택
var bestServer = db.SortedSetRangeByRank("gameservers:available", 0, 0);
```

---

## 7. 매칭 큐 (Matchmaking Queue)

### 키 패턴
```
matchmaking:{game_mode}
```

### 데이터 타입
Sorted Set

### Score
큐 진입 시간 (UNIX Timestamp)

### 참고
- game_mode는 애플리케이션에서 정의 (예: "standard", "ranked", "custom")
- 게임 장르에 관계없이 범용적으로 사용 가능

### 예시 명령어
```redis
# 큐 진입
ZADD matchmaking:mode1 1670000000 "user123"

# 대기 시간 순 조회
ZRANGE matchmaking:mode1 0 -1 WITHSCORES

# N명 매칭 (가장 오래 기다린 유저부터, 예: 4명)
ZRANGE matchmaking:mode1 0 3

# 매칭 완료 후 큐에서 제거
ZREM matchmaking:mode1 "user123" "user456" "user789" "user012"

# 큐 대기자 수
ZCARD matchmaking:mode1
```

### C# 사용 예시
```csharp
var queueKey = $"matchmaking:{gameMode}";

// 큐 진입
db.SortedSetAdd(queueKey, userId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

// 매칭 가능 유저 조회 (필요한 인원 수만큼)
var requiredPlayers = 4; // 애플리케이션에서 결정
var waitingPlayers = db.SortedSetRangeByRank(queueKey, 0, requiredPlayers - 1);

// 매칭 완료
if (waitingPlayers.Length >= requiredPlayers)
{
    db.SortedSetRemove(queueKey, waitingPlayers);
}
```

---

## 8. 실시간 통계 (Real-time Stats)

### 키 패턴
```
stats:realtime
```

### 데이터 타입
Hash

### 필드
```redis
online_users: INT
active_games: INT
total_players_in_game: INT
lobby_users: INT
matchmaking_users: INT
```

### 예시 명령어
```redis
# 온라인 유저 증가
HINCRBY stats:realtime online_users 1

# 온라인 유저 감소
HINCRBY stats:realtime online_users -1

# 활성 게임 증가
HINCRBY stats:realtime active_games 1

# 전체 통계 조회
HGETALL stats:realtime

# 특정 통계 조회
HGET stats:realtime online_users
```

### C# 사용 예시
```csharp
// 통계 증가
db.HashIncrement("stats:realtime", "online_users");

// 통계 감소
db.HashDecrement("stats:realtime", "active_games");

// 전체 통계 조회
var stats = db.HashGetAll("stats:realtime");
```

---

## 9. 리더보드 (Leaderboard)

### 키 패턴
```
leaderboard:{type}
```

### 데이터 타입
Sorted Set

### Score
점수 (MMR, 킬 수, 승률 등)

### 예시 명령어
```redis
# 점수 추가/업데이트
ZADD leaderboard:mmr 2500 "user123"
ZADD leaderboard:mmr 2300 "user456"
ZADD leaderboard:mmr 2800 "user789"

# 상위 10명 (높은 점수 순)
ZREVRANGE leaderboard:mmr 0 9 WITHSCORES

# 특정 유저 순위 (0-based)
ZREVRANK leaderboard:mmr "user123"

# 특정 유저 점수
ZSCORE leaderboard:mmr "user123"

# 점수 범위 조회
ZREVRANGEBYSCORE leaderboard:mmr 3000 2000 WITHSCORES
```

### C# 사용 예시
```csharp
// 점수 설정
db.SortedSetAdd("leaderboard:mmr", userId, mmrScore);

// 상위 10명 조회
var top10 = db.SortedSetRangeByRankWithScores("leaderboard:mmr", 0, 9, Order.Descending);

// 내 순위 조회
var myRank = db.SortedSetRank("leaderboard:mmr", userId, Order.Descending);
```

---

## 10. 채팅 메시지 캐시 (Chat Messages)

### 키 패턴
```
chat:{room_id}
```

### 데이터 타입
List

### TTL
3600초 (1시간)

### 최대 크기
100개 메시지

### 예시 명령어
```redis
# 메시지 추가 (최신 메시지가 앞)
LPUSH chat:room123 '{"user":"player1","msg":"Hello!","time":1670000000}'

# 최근 100개만 유지
LTRIM chat:room123 0 99

# 최근 메시지 조회
LRANGE chat:room123 0 -1

# 최근 10개 메시지만
LRANGE chat:room123 0 9

# TTL 설정
EXPIRE chat:room123 3600
```

### C# 사용 예시
```csharp
var chatKey = $"chat:{roomId}";

// 메시지 추가
var message = JsonSerializer.Serialize(new {
    user = username,
    msg = chatMessage,
    time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
});

db.ListLeftPush(chatKey, message);
db.ListTrim(chatKey, 0, 99);  // 최대 100개
db.KeyExpire(chatKey, TimeSpan.FromHours(1));

// 메시지 조회
var messages = db.ListRange(chatKey, 0, -1);
```

---

## 성능 최적화 팁

### 1. 파이프라인 사용
```csharp
var batch = db.CreateBatch();

var task1 = batch.StringSetAsync("key1", "value1");
var task2 = batch.HashSetAsync("key2", "field", "value");
var task3 = batch.SortedSetAddAsync("key3", "member", 100);

batch.Execute();

await Task.WhenAll(task1, task2, task3);
```

### 2. 트랜잭션 사용
```csharp
var tran = db.CreateTransaction();

tran.HashIncrementAsync("room:room123", "current_players");
tran.SetAddAsync("room:room123:players", userId);

bool committed = await tran.ExecuteAsync();
```

### 3. Lua 스크립트 사용
```csharp
var script = @"
    local players = redis.call('SCARD', KEYS[1])
    if players < tonumber(ARGV[1]) then
        redis.call('SADD', KEYS[1], ARGV[2])
        redis.call('HINCRBY', KEYS[2], 'current_players', 1)
        return 1
    else
        return 0
    end
";

var result = db.ScriptEvaluate(script,
    new RedisKey[] { $"room:{roomId}:players", $"room:{roomId}" },
    new RedisValue[] { maxPlayers, userId });
```

### 4. 커넥션 풀링
```csharp
// 싱글톤으로 ConnectionMultiplexer 관리
public class RedisConnectionFactory
{
    private static Lazy<ConnectionMultiplexer> _connection = new Lazy<ConnectionMultiplexer>(() =>
    {
        var config = ConfigurationOptions.Parse("localhost:6379");
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 5000;
        config.SyncTimeout = 5000;
        return ConnectionMultiplexer.Connect(config);
    });

    public static ConnectionMultiplexer Connection => _connection.Value;
    public static IDatabase Database => Connection.GetDatabase();
}
```
