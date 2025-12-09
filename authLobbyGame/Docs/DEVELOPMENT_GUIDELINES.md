# 개발 가이드라인

## 코딩 규칙

### 네이밍 컨벤션
- **클래스**: PascalCase (예: `AuthenticationServer`, `LobbyManager`)
- **메서드**: PascalCase (예: `HandleClientConnection`, `ValidateToken`)
- **변수/필드**: camelCase (예: `userId`, `connectionTimeout`)
- **상수**: UPPER_SNAKE_CASE (예: `MAX_PLAYERS`, `DEFAULT_PORT`)
- **private 필드**: _camelCase (예: `_tcpListener`, `_connectedClients`)

### 패킷 클래스 구조
```csharp
// 모든 패킷은 IPacket 인터페이스 구현
public interface IPacket
{
    PacketType Type { get; }
    byte[] Serialize();
    void Deserialize(byte[] data);
}

// 패킷 타입 열거형
public enum PacketType : ushort
{
    // Lobby packets (0x1000 - 0x1FFF)
    LobbyConnect = 0x1001,
    CreateRoom = 0x1002,
    JoinRoom = 0x1003,

    // Game packets (0x2000 - 0x2FFF)
    GameConnect = 0x2001,
    GameEvent = 0x2002,

    // UDP packets (0x3000 - 0x3FFF)
    PlayerMove = 0x3001,
    StateSync = 0x3002,
}
```

## 프로젝트 구조

### Common 라이브러리
```
Common/
├── Packets/           # 패킷 정의
│   ├── IPacket.cs
│   ├── PacketType.cs
│   ├── LobbyPackets.cs
│   └── GamePackets.cs
├── Network/           # 네트워크 유틸리티
│   ├── TcpClientWrapper.cs
│   └── PacketSerializer.cs
├── Models/            # 공통 데이터 모델
│   ├── User.cs
│   ├── Room.cs
│   └── GameSession.cs
└── Utils/             # 유틸리티
    ├── Logger.cs
    └── Config.cs
```

### 각 서버 프로젝트 구조
```
Server/
├── Handlers/          # 패킷 핸들러
├── Managers/          # 비즈니스 로직 관리자
├── Network/           # 네트워크 레이어
├── Models/            # 서버 특화 모델
├── Config/            # 설정 파일
└── Program.cs         # 진입점
```

## 로깅 규칙

### 로그 레벨
- **Debug**: 개발 중 상세 정보
- **Info**: 일반 정보 (서버 시작, 클라이언트 연결 등)
- **Warning**: 경고 (재연결 시도, 타임아웃 등)
- **Error**: 오류 (예외, 연결 실패 등)
- **Critical**: 치명적 오류 (서버 다운 등)

### 로그 포맷
```
[2025-12-09 14:30:45] [INFO] [LobbyServer] Client connected: 127.0.0.1:52341
[2025-12-09 14:30:46] [ERROR] [GameServer] Failed to process packet: NullReferenceException
```

## 에러 처리

### Try-Catch 사용
```csharp
// 네트워크 작업은 항상 try-catch로 감싸기
try
{
    await client.SendAsync(packet);
}
catch (SocketException ex)
{
    Logger.Error($"Socket error: {ex.Message}");
    DisconnectClient(client);
}
catch (Exception ex)
{
    Logger.Critical($"Unexpected error: {ex.Message}");
}
```

### 예외 전파
- 복구 불가능한 예외는 상위로 전파
- 복구 가능한 예외는 로깅 후 처리
- 클라이언트 연결 관련 예외는 연결 종료

## 비동기 프로그래밍

### async/await 사용
```csharp
// 모든 네트워크 I/O는 비동기
public async Task<bool> SendPacketAsync(IPacket packet)
{
    byte[] data = packet.Serialize();
    await stream.WriteAsync(data, 0, data.Length);
    return true;
}

// CPU-bound 작업은 Task.Run 사용
public async Task ProcessHeavyLogic()
{
    await Task.Run(() => {
        // Heavy computation
    });
}
```

## 테스트

### 단위 테스트
- 각 패킷 직렬화/역직렬화 테스트
- 비즈니스 로직 테스트
- 프레임워크: xUnit 또는 NUnit

### 통합 테스트
- 서버 간 통신 테스트
- 클라이언트-서버 시나리오 테스트
- 로드 테스트

## 설정 관리

### appsettings.json 사용
```json
{
  "Server": {
    "Ip": "0.0.0.0",
    "TcpPort": 7777,
    "UdpPort": 7778,
    "MaxConnections": 1000
  },
  "Logging": {
    "Level": "Info",
    "FilePath": "logs/server.log"
  }
}
```

## Git 규칙

### 커밋 메시지
```
feat: Add lobby server TCP connection handling
fix: Fix packet deserialization bug in GameServer
refactor: Improve connection manager performance
docs: Update network protocol documentation
test: Add unit tests for packet serialization
```

### 브랜치 전략
- `main`: 안정 버전
- `develop`: 개발 중
- `feature/기능명`: 새 기능 개발
- `fix/버그명`: 버그 수정

## 성능 고려사항

### 메모리 관리
- 큰 배열은 ArrayPool 사용
- 불필요한 객체 생성 최소화
- 사용 후 Dispose 호출

### 네트워크 최적화
- UDP는 작은 패킷 유지 (<1KB)
- TCP는 버퍼링하여 전송
- 패킷 압축 고려 (필요시)

## 보안

### 토큰 검증
- 모든 요청에서 토큰 검증
- 만료 시간 확인
- 재사용 공격 방지

### 입력 검증
- 모든 클라이언트 입력 검증
- SQL Injection 방지
- XSS 방지 (채팅 등)

### 암호화
- 패스워드는 해시 저장 (BCrypt)
- 민감한 데이터는 HTTPS 사용
- 게임 데이터는 체크섬 검증
