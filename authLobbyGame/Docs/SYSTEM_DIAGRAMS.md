# 시스템 다이어그램

## 전체 시스템 아키텍처

```mermaid
graph TB
    subgraph "클라이언트"
        C[Client Application]
    end

    subgraph "인증 레이어"
        AUTH[Authentication Server<br/>ASP.NET Core<br/>HTTP/HTTPS]
    end

    subgraph "로비 레이어"
        LOBBY[Lobby Server<br/>C# Socket<br/>TCP]
    end

    subgraph "게임 레이어"
        GAME1[Game Server 1<br/>C# Socket<br/>TCP + UDP ENet]
        GAME2[Game Server 2<br/>C# Socket<br/>TCP + UDP ENet]
        GAME3[Game Server N<br/>C# Socket<br/>TCP + UDP ENet]
    end

    subgraph "데이터베이스 레이어"
        MYSQL[(MySQL<br/>주 DB)]
        REDIS[(Redis<br/>캐시/세션)]
    end

    C -->|1. Login HTTP POST| AUTH
    AUTH -->|2. JWT Token| C
    C -->|3. Connect TCP + Token| LOBBY
    LOBBY -->|4. Verify Token| REDIS
    REDIS -->|5. Valid/Invalid| LOBBY
    LOBBY -->|6. Game Server Info| C
    C -->|7. Connect TCP/UDP| GAME1
    C -->|7. Connect TCP/UDP| GAME2
    C -->|7. Connect TCP/UDP| GAME3

    AUTH <-->|User Data| MYSQL
    AUTH <-->|Session/Token| REDIS
    LOBBY <-->|Room State| REDIS
    LOBBY <-->|Game Records| MYSQL
    LOBBY <-->|Server Status| GAME1
    LOBBY <-->|Server Status| GAME2
    LOBBY <-->|Server Status| GAME3
    GAME1 <-->|Game State| REDIS
    GAME2 <-->|Game State| REDIS
    GAME3 <-->|Game State| REDIS
    GAME1 -.->|Game Results| MYSQL
    GAME2 -.->|Game Results| MYSQL
    GAME3 -.->|Game Results| MYSQL

    style AUTH fill:#e1f5ff
    style LOBBY fill:#fff9e1
    style GAME1 fill:#e8f5e9
    style GAME2 fill:#e8f5e9
    style GAME3 fill:#e8f5e9
```

## 클라이언트 연결 플로우

```mermaid
sequenceDiagram
    participant C as Client
    participant A as Auth Server
    participant L as Lobby Server
    participant G as Game Server

    Note over C,A: 1. 인증 단계
    C->>A: HTTP POST /api/auth/login<br/>{username, password}
    A->>A: Validate Credentials
    A->>C: Response {token, userId}

    Note over C,L: 2. 로비 진입 단계
    C->>L: TCP Connect
    C->>L: LobbyConnect {token}
    L->>A: HTTP POST /api/auth/verify<br/>{token}
    A->>L: Response {valid, userId}
    alt Token Valid
        L->>C: LobbyConnectResponse {success: true}
        L->>C: RoomList {rooms[]}
    else Token Invalid
        L->>C: Error {code: 2002}
        L-->>C: Disconnect
    end

    Note over C,L: 3. 게임 매칭 단계
    C->>L: CreateRoom or JoinRoom
    L->>L: Assign Game Server
    L->>G: TCP: PrepareSession {userId, sessionToken}
    G->>L: SessionReady {udpPort}
    L->>C: JoinRoomResponse<br/>{gameServerIp, gameServerPort, sessionToken}

    Note over C,G: 4. 게임 플레이 단계
    C->>G: TCP Connect
    C->>G: GameConnect {sessionToken}
    G->>G: Validate Session
    G->>C: GameConnectResponse {udpPort}
    C->>G: UDP Connect (ENet)

    loop Game Loop
        C->>G: UDP: PlayerMove {position, rotation}
        G->>C: UDP: StateSync {players[], timestamp}
        C->>G: TCP: GameEvent {eventType, data}
        G->>C: TCP: GameEvent {eventType, data}
    end

    Note over C,G: 5. 게임 종료 단계
    G->>L: TCP: GameSessionEnd {sessionId, result}
    G->>C: TCP: GameEnd {result}
    C->>L: TCP Reconnect (return to lobby)
```

## 서버 간 통신 다이어그램

```mermaid
graph LR
    subgraph "Lobby Server"
        LM[Lobby Manager]
        RM[Room Manager]
        PM[Player Manager]
    end

    subgraph "Auth Server"
        TV[Token Validator]
        UM[User Manager]
    end

    subgraph "Game Servers"
        GS1[Game Server 1]
        GS2[Game Server 2]
        GSN[Game Server N]
    end

    LM -->|HTTP: Verify Token| TV
    TV -->|Token Valid/Invalid| LM

    RM -->|TCP: Assign Session| GS1
    RM -->|TCP: Assign Session| GS2
    RM -->|TCP: Assign Session| GSN

    GS1 -->|TCP: Heartbeat| LM
    GS2 -->|TCP: Heartbeat| LM
    GSN -->|TCP: Heartbeat| LM

    GS1 -->|TCP: Game Result| RM
    GS2 -->|TCP: Game Result| RM
    GSN -->|TCP: Game Result| RM

    style LM fill:#fff9e1
    style RM fill:#fff9e1
    style PM fill:#fff9e1
    style TV fill:#e1f5ff
    style UM fill:#e1f5ff
    style GS1 fill:#e8f5e9
    style GS2 fill:#e8f5e9
    style GSN fill:#e8f5e9
```

## 패킷 처리 플로우

```mermaid
flowchart TD
    START([Network Data Received])
    READ[Read Packet Header<br/>10 bytes]
    PARSE[Parse Header<br/>PacketType, BodyLength, Sequence]
    VALIDATE{Header Valid?}
    READ_BODY[Read Packet Body<br/>BodyLength bytes]
    DESERIALIZE[Deserialize Body<br/>Based on PacketType]
    DISPATCH{Dispatch to Handler}

    HANDLER_LOBBY[Lobby Handler]
    HANDLER_GAME[Game Handler]
    HANDLER_AUTH[Auth Handler]

    PROCESS[Process Logic]
    RESPONSE[Create Response Packet]
    SERIALIZE[Serialize Response]
    SEND[Send to Client]
    END([End])
    ERROR[Log Error & Disconnect]

    START --> READ
    READ --> PARSE
    PARSE --> VALIDATE
    VALIDATE -->|Valid| READ_BODY
    VALIDATE -->|Invalid| ERROR
    READ_BODY --> DESERIALIZE
    DESERIALIZE --> DISPATCH

    DISPATCH -->|0x1xxx| HANDLER_LOBBY
    DISPATCH -->|0x2xxx| HANDLER_GAME
    DISPATCH -->|0x3xxx| HANDLER_AUTH

    HANDLER_LOBBY --> PROCESS
    HANDLER_GAME --> PROCESS
    HANDLER_AUTH --> PROCESS

    PROCESS --> RESPONSE
    RESPONSE --> SERIALIZE
    SERIALIZE --> SEND
    SEND --> END
    ERROR --> END

    style START fill:#e3f2fd
    style END fill:#e3f2fd
    style ERROR fill:#ffebee
    style VALIDATE fill:#fff9c4
    style DISPATCH fill:#fff9c4
```

## 네트워크 프로토콜 레이어

```mermaid
graph TB
    subgraph "Application Layer"
        APP[Game Logic / Business Logic]
    end

    subgraph "Packet Layer"
        PKT[Packet Handler]
        SER[Serializer / Deserializer]
    end

    subgraph "Transport Layer"
        TCP[TCP Socket<br/>Reliable, Ordered]
        UDP[UDP ENet<br/>Fast, Unreliable]
    end

    subgraph "Network Layer"
        NET[Network Interface]
    end

    APP -->|Create Packet| PKT
    PKT -->|Serialize| SER
    SER -->|Critical Data| TCP
    SER -->|Real-time Data| UDP
    TCP --> NET
    UDP --> NET

    NET -->|Receive| TCP
    NET -->|Receive| UDP
    TCP -->|Raw Bytes| SER
    UDP -->|Raw Bytes| SER
    SER -->|Deserialize| PKT
    PKT -->|Dispatch| APP

    style APP fill:#e1f5ff
    style PKT fill:#fff9e1
    style SER fill:#fff9e1
    style TCP fill:#ffebee
    style UDP fill:#e8f5e9
    style NET fill:#f3e5f5
```

## 로비 서버 내부 구조

```mermaid
graph TB
    subgraph "Lobby Server"
        LISTENER[TCP Listener<br/>Port 7777]

        subgraph "Connection Management"
            CONN_MGR[Connection Manager]
            CONN1[Client Connection 1]
            CONN2[Client Connection 2]
            CONNN[Client Connection N]
        end

        subgraph "Business Logic"
            PLAYER_MGR[Player Manager<br/>Online Players]
            ROOM_MGR[Room Manager<br/>Game Rooms]
            MATCH_MGR[Matchmaking Manager]
        end

        subgraph "External Communication"
            AUTH_CLIENT[Auth Server Client<br/>HTTP]
            GAME_CLIENT[Game Server Client<br/>TCP]
        end

        PACKET_HANDLER[Packet Handler]
    end

    LISTENER -->|Accept| CONN_MGR
    CONN_MGR --> CONN1
    CONN_MGR --> CONN2
    CONN_MGR --> CONNN

    CONN1 -->|Packet| PACKET_HANDLER
    CONN2 -->|Packet| PACKET_HANDLER
    CONNN -->|Packet| PACKET_HANDLER

    PACKET_HANDLER --> PLAYER_MGR
    PACKET_HANDLER --> ROOM_MGR
    PACKET_HANDLER --> MATCH_MGR

    PLAYER_MGR <--> AUTH_CLIENT
    ROOM_MGR <--> GAME_CLIENT
    MATCH_MGR <--> ROOM_MGR

    PLAYER_MGR -->|Response| CONN1
    ROOM_MGR -->|Response| CONN2
    MATCH_MGR -->|Response| CONNN

    style LISTENER fill:#e1f5ff
    style CONN_MGR fill:#fff9e1
    style PLAYER_MGR fill:#e8f5e9
    style ROOM_MGR fill:#e8f5e9
    style MATCH_MGR fill:#e8f5e9
```

## 게임 서버 내부 구조

```mermaid
graph TB
    subgraph "Game Server"
        TCP_LISTENER[TCP Listener<br/>Port 8888]
        UDP_HOST[UDP ENet Host<br/>Port 8889]

        subgraph "Connection Management"
            TCP_MGR[TCP Connection Manager]
            UDP_MGR[UDP Peer Manager]
        end

        subgraph "Game Logic"
            SESSION_MGR[Session Manager<br/>Game Sessions]
            ENTITY_MGR[Entity Manager<br/>Game Objects]
            PHYSICS[Physics Engine]
            GAME_LOOP[Game Loop<br/>60 FPS]
        end

        subgraph "External Communication"
            LOBBY_CLIENT[Lobby Server Client<br/>TCP]
        end

        TCP_HANDLER[TCP Packet Handler]
        UDP_HANDLER[UDP Packet Handler]
    end

    TCP_LISTENER -->|Accept| TCP_MGR
    UDP_HOST -->|Receive| UDP_MGR

    TCP_MGR -->|Packet| TCP_HANDLER
    UDP_MGR -->|Packet| UDP_HANDLER

    TCP_HANDLER -->|Events| SESSION_MGR
    UDP_HANDLER -->|State Updates| SESSION_MGR

    SESSION_MGR --> ENTITY_MGR
    ENTITY_MGR --> PHYSICS
    PHYSICS --> GAME_LOOP

    GAME_LOOP -->|State Sync UDP| UDP_MGR
    GAME_LOOP -->|Events TCP| TCP_MGR

    SESSION_MGR <-->|Status| LOBBY_CLIENT

    UDP_MGR -->|Send| UDP_HOST
    TCP_MGR -->|Send| TCP_LISTENER

    style TCP_LISTENER fill:#e1f5ff
    style UDP_HOST fill:#e8f5e9
    style TCP_MGR fill:#fff9e1
    style UDP_MGR fill:#fff9e1
    style SESSION_MGR fill:#ffebee
    style GAME_LOOP fill:#f3e5f5
```

## 데이터베이스 아키텍처

```mermaid
---
config:
  layout: elk
  look: classic
  theme: dark
---
flowchart TB
 subgraph subGraph0["Application Servers"]
        AUTH["Auth Server"]
        LOBBY["Lobby Server"]
        GAME["Game Servers"]
  end
 subgraph subGraph1["MySQL - 주 데이터베이스"]
        USERS[("Users<br>사용자 정보")]
        RECORDS[("GameRecords<br>게임 기록")]
        SESSIONS[("GameSessions<br>세션 기록")]
        SERVERS[("GameServers<br>서버 정보")]
        STATS[("UserStats<br>통계")]
        TOKENS[("RefreshTokens<br>토큰")]
  end
 subgraph subGraph2["Redis - 캐시/세션 DB"]
        R_SESSION["Session Cache<br>활성 세션"]
        R_ONLINE["Online Users<br>온라인 유저"]
        R_ROOM["Room State<br>방 정보"]
        R_QUEUE["Match Queue<br>매칭 큐"]
        R_SERVER["Server Status<br>서버 상태"]
        R_STATS["Real-time Stats<br>실시간 통계"]
  end
    AUTH -- 로그인 검증 --> USERS
    AUTH -- 토큰 발급/검증 --> TOKENS
    AUTH -- 세션 생성 --> R_SESSION
    AUTH -- 온라인 등록 --> R_ONLINE
    LOBBY -- 유저 조회 --> USERS
    LOBBY -- 세션 검증 --> R_SESSION
    LOBBY -- 방 생성/관리 --> R_ROOM
    LOBBY -- 매칭 처리 --> R_QUEUE
    LOBBY -- 서버 조회 --> R_SERVER
    LOBBY -- 접속 상태 갱신 --> R_ONLINE
    LOBBY -- 세션 로그 기록 --> SESSIONS
    GAME -- 세션 검증 --> R_SESSION
    GAME -- 하트비트 전송 --> R_SERVER
    GAME -- 실시간 통계 --> R_STATS
    GAME -- 게임 결과 저장 --> RECORDS
    GAME -- 유저 통계 갱신 --> STATS
    USERS -. 1:N .-> RECORDS & TOKENS
    USERS -. 1:1 .-> STATS
    SESSIONS -. N:1 .-> SERVERS
```

## 상태 전이 다이어그램 (클라이언트)

```mermaid
stateDiagram-v2
    [*] --> Disconnected

    Disconnected --> Authenticating: Start Login
    Authenticating --> Connected_Lobby: Auth Success
    Authenticating --> Disconnected: Auth Failed

    Connected_Lobby --> In_Room: Create/Join Room
    Connected_Lobby --> Disconnected: Logout

    In_Room --> Connecting_Game: Game Server Assigned
    In_Room --> Connected_Lobby: Leave Room

    Connecting_Game --> In_Game: Game Connect Success
    Connecting_Game --> Connected_Lobby: Game Connect Failed

    In_Game --> Game_Playing: Game Started

    Game_Playing --> Game_Ended: Game Finished
    Game_Playing --> Disconnected: Connection Lost

    Game_Ended --> Connected_Lobby: Return to Lobby

    Disconnected --> [*]
```

## 상태 전이 다이어그램 (게임 서버)

```mermaid
stateDiagram-v2
    [*] --> Initializing

    Initializing --> Ready: Server Started
    Ready --> Registering: Register to Lobby
    Registering --> Available: Registration Success
    Registering --> Error: Registration Failed

    Available --> Session_Active: Session Assigned
    Session_Active --> Available: Session Ended
    Session_Active --> Full: Max Capacity Reached

    Full --> Available: Session Ended

    Available --> Maintenance: Maintenance Mode
    Maintenance --> Available: Maintenance Complete

    Error --> Initializing: Retry
    Available --> Shutdown: Shutdown Command
    Full --> Shutdown: Shutdown Command

    Shutdown --> [*]
```

## 패킷 타입 계층 구조

```mermaid
graph TB
    PKT[IPacket Interface]

    PKT --> LOBBY[Lobby Packets<br/>0x1000-0x1FFF]
    PKT --> GAME[Game Packets<br/>0x2000-0x2FFF]
    PKT --> UDP[UDP Packets<br/>0x3000-0x3FFF]

    LOBBY --> L1[LobbyConnect<br/>0x1001]
    LOBBY --> L2[CreateRoom<br/>0x1002]
    LOBBY --> L3[JoinRoom<br/>0x1003]
    LOBBY --> L4[LeaveRoom<br/>0x1004]
    LOBBY --> L5[RoomList<br/>0x1005]

    GAME --> G1[GameConnect<br/>0x2001]
    GAME --> G2[GameEvent<br/>0x2002]
    GAME --> G3[GameStart<br/>0x2003]
    GAME --> G4[GameEnd<br/>0x2004]

    UDP --> U1[PlayerMove<br/>0x3001]
    UDP --> U2[StateSync<br/>0x3002]
    UDP --> U3[PlayerAction<br/>0x3003]
    UDP --> U4[Snapshot<br/>0x3004]

    style PKT fill:#e3f2fd
    style LOBBY fill:#fff9e1
    style GAME fill:#e8f5e9
    style UDP fill:#f3e5f5
```

## 부하 분산 전략

```mermaid
graph TB
    subgraph "Clients"
        C1[Client 1]
        C2[Client 2]
        C3[Client 3]
        CN[Client N]
    end

    subgraph "Load Balancer (Lobby)"
        LB[Load Balancer<br/>Round Robin or<br/>Least Connection]
    end

    subgraph "Game Servers"
        GS1[Game Server 1<br/>Load: 30%]
        GS2[Game Server 2<br/>Load: 60%]
        GS3[Game Server 3<br/>Load: 20%]
    end

    C1 --> LB
    C2 --> LB
    C3 --> LB
    CN --> LB

    LB -->|Assign by Load| GS1
    LB -->|Assign by Load| GS2
    LB -->|Assign by Load| GS3

    GS1 -->|Heartbeat<br/>Load: 30%| LB
    GS2 -->|Heartbeat<br/>Load: 60%| LB
    GS3 -->|Heartbeat<br/>Load: 20%| LB

    style LB fill:#fff9e1
    style GS1 fill:#e8f5e9
    style GS2 fill:#ffebee
    style GS3 fill:#e8f5e9
```

## 에러 처리 플로우

```mermaid
flowchart TD
    ERROR([Error Occurred])
    TYPE{Error Type}

    NETWORK[Network Error]
    AUTH[Authentication Error]
    GAME[Game Logic Error]
    TIMEOUT[Timeout Error]

    LOG[Log Error]
    NOTIFY[Notify Client]
    RETRY{Can Retry?}
    DISCONNECT[Disconnect Client]
    RECONNECT[Attempt Reconnect]
    RECOVER[Recover State]
    END([End])

    ERROR --> TYPE

    TYPE -->|Socket Exception| NETWORK
    TYPE -->|Invalid Token| AUTH
    TYPE -->|Game State| GAME
    TYPE -->|No Response| TIMEOUT

    NETWORK --> LOG
    AUTH --> LOG
    GAME --> LOG
    TIMEOUT --> LOG

    LOG --> NOTIFY
    NOTIFY --> RETRY

    RETRY -->|Yes| RECONNECT
    RETRY -->|No| DISCONNECT

    RECONNECT --> RECOVER
    RECOVER --> END
    DISCONNECT --> END

    style ERROR fill:#ffebee
    style LOG fill:#fff9e1
    style DISCONNECT fill:#ffcdd2
    style RECOVER fill:#c8e6c9
```

## 메모리 풀링 구조

```mermaid
graph TB
    subgraph "Buffer Pool"
        BP[Buffer Pool Manager]
        B1[Buffer 1KB x 100]
        B2[Buffer 4KB x 50]
        B3[Buffer 16KB x 20]
    end

    subgraph "Packet Pool"
        PP[Packet Pool Manager]
        P1[Lobby Packet Pool x 1000]
        P2[Game Packet Pool x 500]
        P3[UDP Packet Pool x 2000]
    end

    subgraph "Application"
        APP[Application Code]
    end

    APP -->|Request Buffer| BP
    BP -->|Rent| B1
    BP -->|Rent| B2
    BP -->|Rent| B3

    APP -->|Request Packet| PP
    PP -->|Rent| P1
    PP -->|Rent| P2
    PP -->|Rent| P3

    B1 -->|Return| BP
    B2 -->|Return| BP
    B3 -->|Return| BP

    P1 -->|Return| PP
    P2 -->|Return| PP
    P3 -->|Return| PP

    style BP fill:#e1f5ff
    style PP fill:#fff9e1
    style APP fill:#e8f5e9
```
