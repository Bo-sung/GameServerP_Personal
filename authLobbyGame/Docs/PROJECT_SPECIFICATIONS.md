# 프로젝트 사양서

## 프로젝트 개요
C#을 사용한 분산 멀티 서버 게임 아키텍처 구현

## 서버 구성

### 1. 인증 서버 (Authentication Server)
- **프레임워크**: ASP.NET Core
- **역할**: 사용자 인증 및 토큰 발급
- **통신**: HTTP/HTTPS (RESTful API)
- **위치**: `/Project/AuthServer`

### 2. 로비 서버 (Lobby Server)
- **프레임워크**: 순수 C# 소켓
- **TCP**: System.Net.Sockets.TcpClient
- **UDP**: ENet-CSharp
- **역할**:
  - 유저 매칭
  - 게임 방 관리
  - 게임 서버 배정
- **위치**: `/Project/LobbyServer`

### 3. 게임 서버 (Game Server)
- **프레임워크**: 순수 C# 소켓
- **TCP**: System.Net.Sockets.TcpClient
- **UDP**: ENet-CSharp
- **역할**:
  - 실시간 게임 세션 관리
  - 게임 로직 처리
  - 플레이어 상태 동기화
- **위치**: `/Project/GameServer`

## 네트워크 프로토콜

### TCP 사용 케이스
- 인증 정보 전송
- 중요한 게임 이벤트 (아이템 획득, 레벨업 등)
- 채팅 메시지
- 서버 간 통신

### UDP 사용 케이스 (ENet-CSharp)
- 플레이어 위치 동기화
- 빠른 액션 입력
- 실시간 게임 상태 업데이트

## 기술 스택
- **언어**: C# (.NET 6.0 이상 권장)
- **인증 서버**: ASP.NET Core
- **로비/게임 서버**: .NET Console Application
- **UDP 라이브러리**: ENet-CSharp
- **TCP**: System.Net.Sockets.TcpClient
- **주 데이터베이스**: MySQL 8.0+
- **캐싱/세션 DB**: Redis 7.0+

## 데이터베이스 아키텍처

### MySQL (주 데이터베이스)
- **역할**: 영구 데이터 저장
- **저장 데이터**:
  - 사용자 정보 (Users)
  - 게임 기록 (GameRecords)
  - 통계 데이터 (Statistics)
- **사용 서버**: 인증 서버, 로비 서버

### Redis (캐싱 및 세션 DB)
- **역할**: 실시간 데이터 캐싱 및 세션 관리
- **저장 데이터**:
  - 활성 세션 정보
  - 온라인 유저 목록
  - 방 정보 (Room State)
  - 게임 서버 상태
  - 매칭 큐
- **사용 서버**: 로비 서버, 게임 서버
- **데이터 TTL**: 세션별 만료 시간 설정

## 폴더 구조
```
/
├── Docs/               # 모든 문서
│   ├── PROJECT_SPECIFICATIONS.md
│   ├── ARCHITECTURE.md
│   ├── NETWORK_PROTOCOL.md
│   ├── SERIALIZATION_SPEC.md
│   ├── SYSTEM_DIAGRAMS.md
│   ├── DATABASE_SCHEMA.md
│   └── DEVELOPMENT_GUIDELINES.md
│
└── Project/            # 모든 프로젝트 코드
    ├── AuthServer/
    ├── LobbyServer/
    ├── GameServer/
    ├── Common/         # 공통 라이브러리
    └── Client/         # 테스트 클라이언트
```

## 개발 우선순위
1. 공통 라이브러리 (프로토콜, 유틸리티)
2. 인증 서버 (기본 API)
3. 로비 서버 (TCP 통신)
4. 게임 서버 (TCP + UDP 통신)
5. 통합 테스트

## 참고사항
- 모든 서버는 독립적으로 실행 가능해야 함
- 서버 간 통신은 명확한 프로토콜 정의 필요
- 확장성을 고려한 설계
