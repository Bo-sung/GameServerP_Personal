# 범용 멀티플레이어 서버 프레임워크

InterPlanetery_Server 프로젝트에서 추출한 범용 멀티플레이어 게임 서버 프레임워크입니다.

## 📋 개요

이 프레임워크는 게임 특화 로직을 제거하고, TCP 기반 멀티플레이어 서버의 핵심 기능만을 포함합니다.
다양한 멀티플레이어 게임에서 재사용 가능하도록 설계되었습니다.

## 🎯 주요 기능

### 1. 인증 시스템
- 회원가입 (일반 / 자동 게스트)
- 로그인 / 로그아웃
- MySQL 기반 사용자 관리

### 2. 로비 시스템
- 룸 목록 조회
- 페이지네이션 지원
- 실시간 룸 정보 업데이트

### 3. 룸 시스템
- 룸 생성 / 입장 / 퇴장
- 플레이어 준비 상태 관리
- 자동 빈 룸 정리
- 룸 내 채팅

### 4. 네트워크
- TCP 기반 바이너리 프로토콜
- JSON 파라미터 직렬화
- 하트비트 및 타임아웃 관리
- 스레드 세이프 전송

### 5. 세션 관리
- 클라이언트 세션 생명주기 관리
- 타임아웃 자동 감지 (30초)
- 안전한 연결 종료

## 🏗️ 프로젝트 구조

```
GenericServer/
├── CommonLib/              # 공통 라이브러리
│   ├── Commands/
│   │   └── Command.cs      # 커맨드 인터페이스
│   ├── AppConfig.cs        # 설정 관리
│   ├── CommonEnum.cs       # 공통 열거형
│   ├── Graph.cs            # 그래프 유틸리티
│   ├── Protocol.cs         # 프로토콜 직렬화/역직렬화
│   ├── ProtocolTypes.cs    # 프로토콜 타입 정의
│   ├── SingletonBase.cs    # 싱글톤 베이스
│   └── Vector.cs           # 벡터 유틸리티
│
└── BaseServer/             # 서버 프레임워크
    ├── Core/
    │   └── Game/
    │       ├── Entities/
    │       │   ├── Room.cs         # 룸 관리
    │       │   └── RoomUser.cs     # 룸 사용자
    │       ├── Managers/
    │       │   └── RoomManager.cs  # 룸 매니저
    │       ├── Session/
    │       │   └── ClientSession.cs # 클라이언트 세션
    │       └── ICommandSender.cs
    ├── Database/           # DB 연결 및 인증
    ├── Network/            # TCP 통신
    ├── Utils/              # 유틸리티
    └── Program.cs          # 진입점
```

## 🚀 시작하기

### 필수 요구사항
- .NET 8.0 SDK
- MySQL 서버

### 빌드

```bash
cd extraction/Server
dotnet build GenericServer.sln
```

### 실행

```bash
cd BaseServer
dotnet run
```

### 설정

`appsettings.json` 파일에서 데이터베이스 연결 정보를 설정하세요:

```json
{
  "databases": {
    "table": {
      "server": "localhost",
      "userId": "root",
      "password": "your_password",
      "databaseName": "your_tabledb",
      "port": 3306
    },
    "auth": {
      "server": "localhost",
      "userId": "root",
      "password": "your_password",
      "databaseName": "your_authdb",
      "port": 3306
    }
  }
}
```

## 🎮 게임 프로젝트에 적용하기

### 1. 커스텀 Room 클래스 생성

```csharp
public class MyGameRoom : Room
{
    private MyGameInstance gameInstance;
    
    public MyGameRoom(string roomId, int mapID = 0) : base(roomId, mapID)
    {
    }
    
    protected override void OnAllPlayersReady()
    {
        // 모든 플레이어가 준비되었을 때 게임 시작
        gameInstance = new MyGameInstance();
        gameInstance.Initialize(this, MapID);
        gameInstance.StartGame();
    }
}
```

### 2. RoomManager 수정

```csharp
public class RoomManager
{
    public Room CreateRoom()
    {
        int roomId = Interlocked.Increment(ref m_roomIdCounter);
        string roomIdString = $"ROOM_{roomId:D4}";

        // MyGameRoom 사용
        Room room = new MyGameRoom(roomIdString);
        
        if (m_rooms.TryAdd(roomIdString, room))
        {
            LogWithTimestamp($"[RoomManager] Room created: {roomIdString}");
            return room;
        }

        return null;
    }
}
```

### 3. 커스텀 프로토콜 추가

`ProtocolTypes.cs`에 게임 특화 프로토콜을 추가하세요:

```csharp
public static class ProtocolType
{
    // 기존 프로토콜...
    
    // 게임 특화 프로토콜
    public const int GAME_MOVE_UNIT = 30200;
    public const int GAME_ATTACK = 30201;
    public const int GAME_BUILD = 30202;
}
```

### 4. 프로토콜 핸들러 등록

```csharp
protected override void RegisterProtocolHandlers()
{
    base.RegisterProtocolHandlers();
    
    // 게임 특화 핸들러 등록
    m_protocolHandler.RegisterHandler(ProtocolType.GAME_MOVE_UNIT, Handle_MoveUnit);
    m_protocolHandler.RegisterHandler(ProtocolType.GAME_ATTACK, Handle_Attack);
}
```

## 📡 프로토콜 구조

### 메시지 형식

```
[4 bytes] 전체 크기 (헤더 포함)
[4 bytes] 프로토콜 타입
[8 bytes] 타임스탬프
[2 bytes] 파라미터 개수
[N bytes] JSON 파라미터
```

### 사용 예시

```csharp
// 클라이언트 → 서버
Protocol request = new Protocol(ProtocolType.REQUEST_LOGIN)
    .AddParam("username", "player1")
    .AddParam("password", "password123");
await SendAsync(request.Serialize());

// 서버 → 클라이언트
Response response = new Response(ProtocolType.REQUEST_LOGIN, StateCode.SUCCESS);
response.AddParam("sessionId", "abc123");
await SendAsync(response.Serialize());
```

## 🔧 확장 가능한 설계

### Room 클래스
- `OnAllPlayersReady()`: 모든 플레이어 준비 완료 시 호출
- `Dispose()`: 룸 정리 시 호출
- 상속하여 게임 로직 추가 가능

### RoomUser 클래스
- `RegistorProtos()`: 프로토콜 핸들러 등록
- `UnRegistorProtos()`: 프로토콜 핸들러 해제
- 상속하여 플레이어 특화 기능 추가 가능

## 📝 라이선스

이 프로젝트는 InterPlanetery_Server에서 추출되었습니다.

## 🤝 기여

버그 리포트 및 기능 제안은 이슈로 등록해주세요.

## 📞 문의

프로젝트 관련 문의사항이 있으시면 이슈를 생성해주세요.
