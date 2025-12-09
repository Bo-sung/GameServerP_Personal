# Auth-Lobby-Game 서버 프로젝트

C#을 사용한 분산 멀티 서버 게임 아키텍처 프로젝트

## 프로젝트 개요

인증, 로비, 게임 서버를 분리하여 확장 가능한 멀티플레이어 게임 서버 시스템을 구축합니다.

### 주요 특징
- **3-Tier 서버 아키텍처**: 인증 / 로비 / 게임 서버 분리
- **하이브리드 프로토콜**: TCP (신뢰성) + UDP (실시간성)
- **이중 데이터베이스**: MySQL (영구 저장) + Redis (실시간 캐싱)
- **플랫폼 독립적 직렬화**: 언어/플랫폼 간 호환성 보장
- **게임 장르 독립적**: 어떤 게임에서든 사용 가능한 범용 설계
- **자동 리소스 관리**: 비활성 방/세션 자동 제거

## 기술 스택

### 서버
- **인증 서버**: ASP.NET Core (HTTP/HTTPS)
- **로비/게임 서버**: .NET 6.0+ Console Application
- **TCP**: System.Net.Sockets.TcpClient
- **UDP**: ENet-CSharp

### 데이터베이스
- **주 데이터베이스**: MySQL 8.0+
  - 사용자 정보, 게임 기록, 통계 데이터
- **캐싱 DB**: Redis 7.0+
  - 세션 관리, 온라인 유저, 방 정보, 매칭 큐

## 서버 구조

```
클라이언트
    ↓ (HTTP/HTTPS)
인증 서버 (ASP.NET Core)
    ↓ (JWT Token)
로비 서버 (C# TCP)
    ↓ (Server Assignment)
게임 서버 (C# TCP + UDP ENet)
```

### 1. 인증 서버
- **역할**: 사용자 인증, JWT 토큰 발급
- **프로토콜**: HTTP/HTTPS
- **데이터베이스**: MySQL (users, refresh_tokens), Redis (sessions)

### 2. 로비 서버
- **역할**: 방 관리, 매칭, 게임 서버 배정
- **프로토콜**: TCP
- **데이터베이스**: Redis (rooms, matchmaking queue, online users)

### 3. 게임 서버
- **역할**: 실시간 게임 세션 관리, 게임 로직 처리
- **프로토콜**: TCP (중요 이벤트) + UDP (실시간 동기화)
- **데이터베이스**: Redis (game state), MySQL (game results)

## 네트워크 프로토콜

### TCP 사용
- 인증 정보 전송
- 중요한 게임 이벤트 (아이템 획득, 레벨업)
- 채팅 메시지
- 서버 간 통신

### UDP 사용 (ENet)
- 플레이어 위치 동기화
- 빠른 액션 입력
- 실시간 게임 상태 업데이트

## 데이터베이스 구조

### MySQL 테이블
- `users`: 사용자 계정 정보
- `game_records`: 게임 플레이 기록
- `game_sessions`: 세션 로그
- `game_servers`: 서버 메타데이터
- `user_stats`: 유저 통계 (MMR, 승률 등)
- `refresh_tokens`: 리프레시 토큰

### Redis 데이터
- `session:{id}`: 활성 세션 (Hash, TTL: 1시간)
- `online_users`: 온라인 유저 목록 (Sorted Set)
- `room:{id}`: 방 정보 (Hash, TTL: 2시간)
- `matchmaking:{type}`: 매칭 큐 (Sorted Set)
- `gameserver:{id}`: 게임 서버 상태 (Hash, TTL: 1분)
- `stats:realtime`: 실시간 통계 (Hash)
- `leaderboard:{type}`: 리더보드 (Sorted Set)

## 클라이언트 연결 플로우

1. **인증 단계**
   - 클라이언트 → 인증 서버: 로그인 (HTTP POST)
   - 인증 서버 → 클라이언트: JWT 토큰

2. **로비 진입**
   - 클라이언트 → 로비 서버: TCP 연결 + 토큰
   - 로비 서버 → Redis: 세션 검증
   - 로비 서버 → 클라이언트: 방 목록

3. **게임 매칭**
   - 클라이언트 → 로비 서버: 방 생성/참가
   - 로비 서버 → 게임 서버 선택 (부하 기반)
   - 로비 서버 → 클라이언트: 게임 서버 정보

4. **게임 플레이**
   - 클라이언트 → 게임 서버: TCP 연결
   - 클라이언트 ↔ 게임 서버: UDP 채널 (ENet)
   - 게임 로직 실행 및 실시간 동기화

5. **게임 종료**
   - 게임 서버 → MySQL: 결과 저장
   - 클라이언트 → 로비 서버: 재연결

## 문서 구조

### 핵심 문서
- **[PROJECT_SPECIFICATIONS.md](PROJECT_SPECIFICATIONS.md)**: 프로젝트 전체 사양
- **[ARCHITECTURE.md](ARCHITECTURE.md)**: 시스템 아키텍처
- **[SYSTEM_DIAGRAMS.md](SYSTEM_DIAGRAMS.md)**: Mermaid 다이어그램 (15개)

### 기술 문서
- **[NETWORK_PROTOCOL.md](NETWORK_PROTOCOL.md)**: 네트워크 프로토콜 명세
- **[SERIALIZATION_SPEC.md](SERIALIZATION_SPEC.md)**: 직렬화 슈도 코드
- **[DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)**: MySQL 스키마 + Redis 구조
- **[REDIS_DATA_STRUCTURES.md](REDIS_DATA_STRUCTURES.md)**: Redis 사용법 상세

### 개발 가이드
- **[DEVELOPMENT_GUIDELINES.md](DEVELOPMENT_GUIDELINES.md)**: 코딩 규칙, 성능, 보안

## 패킷 타입 (예시)

### 로비 패킷 (0x1000 - 0x1FFF)
- `0x1001`: LobbyConnect
- `0x1002`: CreateRoom
- `0x1003`: JoinRoom
- `0x1004`: LeaveRoom
- `0x1005`: RoomList

### 게임 패킷 (0x2000 - 0x2FFF)
- `0x2001`: GameConnect
- `0x2002`: GameEvent
- `0x2003`: GameStart
- `0x2004`: GameEnd

### UDP 패킷 (0x3000 - 0x3FFF)
- `0x3001`: PlayerMove
- `0x3002`: StateSync
- `0x3003`: PlayerAction

## 보안

### 인증
- 비밀번호: BCrypt 해싱 + Salt
- 토큰: JWT (Access Token) + Refresh Token
- 세션: Redis TTL 기반 자동 만료

### 통신
- 인증 서버: HTTPS
- 게임 데이터: 체크섬 검증
- 입력 검증: 모든 클라이언트 입력 서버측 검증

## 성능 최적화

### 메모리
- ArrayPool 사용 (큰 배열)
- 객체 풀링 (패킷, 버퍼)
- Redis 메모리 정책: allkeys-lru

### 네트워크
- TCP: 버퍼링 후 전송
- UDP: 작은 패킷 유지 (<1KB)
- 파이프라인: Redis 명령 배치 실행

### 데이터베이스
- MySQL: 인덱스 최적화, 파티셔닝
- Redis: 파이프라인, Lua 스크립트
- 커넥션 풀링

## 확장성

### 수평 확장
- 게임 서버: 여러 인스턴스 실행 가능
- 로드 밸런싱: Redis Sorted Set 기반 부하 분산
- 하트비트: 게임 서버 상태 실시간 모니터링

### 장애 대응
- 서버 독립성: 각 서버 개별 재시작
- 게임 서버 다운: 로비에서 자동 재배정
- 세션 복구: Redis 지속성 (RDB + AOF)

## 프로젝트 구조

```
/
├── Docs/                    # 모든 문서
│   ├── README.md
│   ├── PROJECT_SPECIFICATIONS.md
│   ├── ARCHITECTURE.md
│   ├── SYSTEM_DIAGRAMS.md
│   ├── NETWORK_PROTOCOL.md
│   ├── SERIALIZATION_SPEC.md
│   ├── DATABASE_SCHEMA.md
│   ├── REDIS_DATA_STRUCTURES.md
│   └── DEVELOPMENT_GUIDELINES.md
│
└── Project/                 # 프로젝트 코드 (예정)
    ├── Common/              # 공통 라이브러리
    ├── AuthServer/          # 인증 서버
    ├── LobbyServer/         # 로비 서버
    ├── GameServer/          # 게임 서버
    └── Client/              # 테스트 클라이언트
```

## 개발 우선순위

1. **공통 라이브러리** - 프로토콜, 직렬화, 유틸리티
2. **인증 서버** - 기본 로그인 API
3. **로비 서버** - TCP 통신, 방 관리
4. **게임 서버** - TCP + UDP 통신, 게임 로직
5. **통합 테스트** - 전체 시스템 테스트

## 게임 장르 독립적 설계

이 프로젝트는 특정 게임 장르에 종속되지 않도록 설계되었습니다:

### 범용 기능
- **game_mode**: 애플리케이션에서 자유롭게 정의 ("standard", "ranked", "custom" 등)
- **JSON stats**: 게임별 커스텀 통계 데이터 저장 가능
- **유연한 방 시스템**: 최대 인원, 상태, 비공개 여부 등 범용 속성만 제공

### 적용 가능한 게임 예시
- **FPS/TPS**: stats에 kills, deaths, headshots 저장
- **MOBA**: kills, deaths, assists, gold 저장
- **퍼즐 게임**: moves, time, hints_used 저장
- **보드 게임**: turns, pieces_captured 저장
- **레이싱**: lap_time, position, boost_used 저장

## 자동 리소스 관리

### 비활성 방 자동 제거
- **감지 주기**: 로비 서버에서 5분마다 스캔
- **제거 조건**:
  - status가 "waiting"
  - last_activity가 30분 이상 지남
- **안전장치**: Redis TTL 2시간 (최종 보호)

### 세션 관리
- **Access Token**: 1시간 자동 만료
- **Refresh Token**: 30일 자동 만료
- **게임 서버 상태**: 하트비트 중단 시 1분 후 자동 제거

## 참고사항

- 모든 서버는 독립적으로 실행 가능
- 직렬화는 플랫폼 독립적 (C#, Unity, 다른 언어 간 호환)
- Little Endian 사용 (모든 멀티바이트 값)
- UTF-8 인코딩 (모든 문자열)
- Row-major 행렬 (Matrix4x4 등)
- 게임 특화 로직은 최소화, 공통 기능에 집중

## 라이선스

TBD

## 기여

TBD
