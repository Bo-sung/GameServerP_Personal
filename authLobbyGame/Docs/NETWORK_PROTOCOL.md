# 네트워크 프로토콜 명세

## 프로토콜 기본 원칙

### 패킷 구조
```
[Header][Body]

Header:
- PacketType (2 bytes): 패킷 유형
- BodyLength (4 bytes): Body 크기
- Sequence (4 bytes): 패킷 시퀀스 번호

Body:
- JSON 또는 Binary 직렬화 데이터
```

## TCP 프로토콜

### 인증 서버 API (HTTP/REST)

#### POST /api/auth/login
요청:
```json
{
  "username": "string",
  "password": "string"
}
```

응답:
```json
{
  "success": true,
  "token": "jwt_token_string",
  "userId": "user_id",
  "expiresIn": 3600
}
```

#### POST /api/auth/verify
요청:
```json
{
  "token": "jwt_token_string"
}
```

응답:
```json
{
  "valid": true,
  "userId": "user_id"
}
```

### 로비 서버 TCP 패킷

#### LobbyConnect (0x1001)
```
Client → Server
{
  "token": "jwt_token_string"
}

Server → Client
{
  "success": true,
  "userId": "user_id",
  "serverTime": timestamp
}
```

#### CreateRoom (0x1002)
```
Client → Server
{
  "roomName": "string",
  "maxPlayers": int,
  "isPrivate": bool
}

Server → Client
{
  "success": true,
  "roomId": "room_id"
}
```

#### JoinRoom (0x1003)
```
Client → Server
{
  "roomId": "room_id"
}

Server → Client
{
  "success": true,
  "gameServerIp": "ip_address",
  "gameServerPort": port,
  "sessionToken": "string"
}
```

### 게임 서버 TCP 패킷

#### GameConnect (0x2001)
```
Client → Server
{
  "sessionToken": "string",
  "userId": "user_id"
}

Server → Client
{
  "success": true,
  "udpPort": port
}
```

#### GameEvent (0x2002)
```
Bidirectional
{
  "eventType": "string",
  "eventData": object
}
```

## UDP 프로토콜 (ENet)

### PlayerMove (0x3001)
```
Client → Server
{
  "posX": float,
  "posY": float,
  "posZ": float,
  "rotation": float
}
```

### StateSync (0x3002)
```
Server → Client
{
  "players": [
    {
      "playerId": "string",
      "posX": float,
      "posY": float,
      "posZ": float,
      "rotation": float
    }
  ],
  "timestamp": long
}
```

### PlayerAction (0x3003)
```
Client → Server
{
  "actionType": int,
  "targetId": "string",
  "parameters": object
}
```

## 에러 코드

| 코드 | 설명 |
|-----|------|
| 1000 | 성공 |
| 2001 | 인증 실패 |
| 2002 | 잘못된 토큰 |
| 2003 | 만료된 토큰 |
| 3001 | 방 생성 실패 |
| 3002 | 방이 가득 찼음 |
| 3003 | 방을 찾을 수 없음 |
| 4001 | 게임 서버 연결 실패 |
| 4002 | 잘못된 세션 |
| 5000 | 서버 내부 오류 |

## 직렬화 방식
- **인증 서버**: JSON (HTTP Body)
- **로비 서버**: JSON (간단한 구조) 또는 MessagePack (성능 필요시)
- **게임 서버**:
  - TCP: MessagePack 또는 Protobuf
  - UDP: 바이너리 직렬화 (성능 최적화)
