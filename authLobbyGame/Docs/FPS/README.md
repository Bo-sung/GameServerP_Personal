# FPS 게임 서버 구현 가이드

이 문서는 상위 프레임워크를 기반으로 FPS 게임 서버를 구현하는 방법을 설명합니다.

## 개요

범용 게임 서버 프레임워크를 활용하여 FPS 게임의 특화 기능을 구현합니다.

### FPS 게임 특징
- **실시간 동기화**: UDP를 통한 플레이어 위치/회전 동기화
- **히트 판정**: 서버 권한 기반 정확한 히트 판정
- **무기 시스템**: 다양한 무기 및 발사체 관리
- **맵 관리**: 스폰 포인트, 아이템 배치, 충돌 검증

## 문서 구조

### 핵심 문서
- **[FPS_GAME_SPECIFICATION.md](FPS_GAME_SPECIFICATION.md)**: FPS 게임 사양
- **[FPS_NETWORK_PACKETS.md](FPS_NETWORK_PACKETS.md)**: FPS 전용 패킷 정의
- **[FPS_GAME_LOGIC.md](FPS_GAME_LOGIC.md)**: 게임 로직 및 규칙

### 기술 문서
- **[FPS_DATABASE_EXTENSION.md](FPS_DATABASE_EXTENSION.md)**: FPS 전용 DB 확장
- **[FPS_SYNCHRONIZATION.md](FPS_SYNCHRONIZATION.md)**: 실시간 동기화 전략
- **[FPS_HIT_DETECTION.md](FPS_HIT_DETECTION.md)**: 히트 판정 시스템

## 프레임워크 활용

### 상위 프레임워크에서 제공
- ✅ 인증 시스템 (로그인/회원가입)
- ✅ 로비 시스템 (방 생성/참가)
- ✅ TCP/UDP 네트워크 기반
- ✅ 플랫폼 독립적 직렬화
- ✅ 세션 관리 및 보안

### FPS 게임에서 구현
- 🎮 플레이어 이동 및 회전 동기화
- 🎮 무기 발사 및 히트 판정
- 🎮 맵 로딩 및 스폰 관리
- 🎮 게임 모드 (데스매치, 팀 데스매치 등)
- 🎮 킬/데스 통계 및 스코어보드

## 게임 모드

### 지원 모드
1. **Free For All (FFA)**: 개인전
2. **Team Deathmatch (TDM)**: 팀전
3. **Capture The Flag (CTF)**: 깃발 탈취전

## 프로젝트 구조

```
/Project
└── FPS/
    ├── FPSGameServer/          # FPS 게임 서버 (게임 로직)
    │   ├── GameLogic/          # 게임 규칙
    │   ├── Weapons/            # 무기 시스템
    │   ├── Maps/               # 맵 데이터
    │   └── Synchronization/    # 동기화 로직
    │
    ├── FPSCommon/              # FPS 공통 라이브러리
    │   ├── Packets/            # FPS 패킷 정의
    │   └── Data/               # FPS 데이터 구조
    │
    └── FPSClient/              # 테스트 클라이언트
```

## 주요 특징

### 1. 서버 권한 기반 검증
- 모든 플레이어 행동은 서버에서 검증
- 클라이언트는 입력만 전송, 결과는 서버가 결정
- 치트 방지를 위한 서버 측 히트 판정

### 2. 하이브리드 네트워크
- **TCP**: 중요 이벤트 (킬, 데스, 게임 시작/종료)
- **UDP**: 실시간 데이터 (위치, 회전, 발사)

### 3. 렉 보상 (Lag Compensation)
- 과거 위치 기록 및 보간
- 클라이언트 지연 시간 고려
- 공정한 히트 판정

### 4. 확장 가능한 무기 시스템
- JSON 기반 무기 데이터
- Hitscan / Projectile 지원
- 반동, 연사력, 데미지 커스터마이징

## 성능 목표

- **틱레이트**: 64 tick/sec (15.625ms)
- **최대 플레이어**: 방당 16명
- **레이턴시**: 50ms 이하에서 최적화
- **패킷 크기**: UDP 패킷 512바이트 이하

## 개발 우선순위

1. **기본 이동 동기화** - 위치/회전 전송 및 보간
2. **무기 발사** - Hitscan 기반 즉시 판정
3. **히트 판정** - Raycast 기반 히트 검증
4. **게임 모드** - FFA 모드 구현
5. **통계 및 스코어보드** - 킬/데스 추적

## 참고사항

- 상위 프레임워크의 game_mode를 활용 ("ffa", "tdm", "ctf")
- JSON stats 필드에 FPS 통계 저장 (kills, deaths, headshots)
- 클라이언트 예측 + 서버 조정 방식 권장
- 물리 엔진은 클라이언트/서버 동일하게 유지
