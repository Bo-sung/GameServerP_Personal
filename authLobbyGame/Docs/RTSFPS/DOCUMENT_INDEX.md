# FPS/RTS 하이브리드 게임 - 문서 완전 인덱스

## 📚 전체 문서 구조

### Tier 1: 핵심 게임 설계 (먼저 읽어야 함)
1. **README.md** - 프로젝트 개요 및 아키텍처
2. **HYBRID_GAME_SPECIFICATION.md** - 게임 규칙, 승리 조건, 시스템 설명
3. **HYBRID_NETWORK_ARCHITECTURE.md** - 서버 구성, 통신 구조

### Tier 2: 기술 명세 (구현 전 필독)
4. **HYBRID_NETWORK_PACKETS.md** - 패킷 정의 및 직렬화
5. **BOT_AI_SYSTEM.md** - AI FSM, 명중률, 제압 시스템
6. **HYBRID_GAME_LOGIC.md** - 각 구성요소의 게임 루프 및 로직

### Tier 3: 운영 & 최적화 (배포 및 튜닝)
7. **HYBRID_SYNCHRONIZATION.md** - 클라이언트 예측, 라그 보상, 동기화
8. **HYBRID_BALANCING.md** - 난이도, 무기, 스쿼드 능력 밸런싱
9. **HYBRID_DATABASE_EXTENSION.md** - 통계, 리플레이, 성과 추적

### Tier 4: 실구현 (코딩 가이드)
10. **BOT_CONTROLLER_IMPLEMENTATION.md** - C# 프로젝트 설정 및 구현
11. **HYBRID_TESTING.md** - 단위/통합/성능/E2E 테스트

---

## 🎯 읽기 순서 (역할별)

### 👨‍💼 프로젝트 매니저
1. README.md (프로젝트 개요)
2. HYBRID_GAME_SPECIFICATION.md (게임 디자인)
3. HYBRID_BALANCING.md (밸런싱 목표)
4. HYBRID_DATABASE_EXTENSION.md (데이터 추적)

### 🎮 게임 디자이너
1. HYBRID_GAME_SPECIFICATION.md (완전 숙독)
2. BOT_AI_SYSTEM.md (AI 디자인)
3. HYBRID_BALANCING.md (게임 밸런싱)
4. HYBRID_GAME_LOGIC.md (구현 이해)

### 💻 백엔드 개발자 (C# Bot Controller)
1. HYBRID_NETWORK_ARCHITECTURE.md (아키텍처 이해)
2. HYBRID_NETWORK_PACKETS.md (패킷 포맷)
3. BOT_AI_SYSTEM.md (AI 로직)
4. BOT_CONTROLLER_IMPLEMENTATION.md (구현 가이드)
5. HYBRID_SYNCHRONIZATION.md (동기화)

### 🎮 Unreal Engine 개발자
1. HYBRID_GAME_SPECIFICATION.md (게임 이해)
2. HYBRID_NETWORK_ARCHITECTURE.md (Unreal 서버 구성)
3. HYBRID_NETWORK_PACKETS.md (패킷 처리)
4. HYBRID_GAME_LOGIC.md (게임 루프)
5. HYBRID_SYNCHRONIZATION.md (네트워크 처리)

### 🧪 QA & 테스트 엔지니어
1. HYBRID_GAME_SPECIFICATION.md (게임 규칙)
2. HYBRID_BALANCING.md (밸런싱 기준)
3. HYBRID_TESTING.md (테스트 계획)
4. HYBRID_DATABASE_EXTENSION.md (통계 추적)

### 📊 DevOps & 운영팀
1. HYBRID_NETWORK_ARCHITECTURE.md (시스템 구성)
2. BOT_CONTROLLER_IMPLEMENTATION.md (배포)
3. HYBRID_DATABASE_EXTENSION.md (데이터 관리)
4. HYBRID_TESTING.md (성능 테스트)

---

## 📖 문서별 핵심 내용 요약

### 1️⃣ HYBRID_GAME_SPECIFICATION.md
**목적**: 게임 설계 및 규칙 정의
**주요 섹션**:
- 게임 개요 (4v100 비대칭)
- 3가지 승리 조건
- 4개 FPS 클래스 (Leader, Medic, Engineer, Sniper)
- 5개 무기 시스템
- 100개 AI 봇 시스템
- 점수/보상 시스템
- 게임 플로우 (2분 준비 → 2-20분 진행)

### 2️⃣ HYBRID_NETWORK_ARCHITECTURE.md
**목적**: 서버 아키텍처 및 통신 설계
**주요 섹션**:
- 3계층 서버 구성
- Unreal 데디케이티드 서버 (Port 7777)
- C# Bot Controller (Port 7779)
- RTS 클라이언트 (Port 7778)
- 통신 프로토콜 및 주기 (60Hz/30Hz/10Hz)
- 대역폭 계산 (~118 KB/s)
- 동기화 메커니즘

### 3️⃣ HYBRID_NETWORK_PACKETS.md
**목적**: 패킷 형식 및 직렬화 정의
**주요 섹션**:
- 패킷 범위 할당 (0x5000-0x5FFF)
- FPS ↔ Unreal 패킷 (PlayerMovement, WeaponFire)
- Unreal ↔ C# Bot 패킷 (BotState, BotInput)
- RTS ↔ C# 패킷 (RTSCommand, UnitStatusUpdate)
- 제어 패킷 (Ping, GameState)
- 대역폭 최적화 기법 (압축, 델타)
- 신뢰성 정책 (TCP vs UDP)

### 4️⃣ BOT_AI_SYSTEM.md
**목적**: AI 봇 시스템 명세
**주요 섹션**:
- 5개 상태 FSM (Patrol, Investigate, Combat, Retreat, Healing)
- 관찰시간 기반 명중률 (10%→25%→50%→75%)
- 제압 시스템 (Light/Moderate/Heavy)
- 개별 봇 프로필 (Aggression, Accuracy, Courage, ReactionTime)
- 엄폐/측면 공격 전술
- 경로 계산 (A* 알고리즘)
- 성능 최적화 팁

### 5️⃣ HYBRID_GAME_LOGIC.md
**목적**: 각 구성요소의 게임 루프 구현
**주요 섹션**:
- Unreal 게임 모드 루프 (Warmup → InProgress → Finished)
- FPS 클라이언트 입력 처리 (60Hz)
- FPS 스쿼드 능력 시스템
- RTS 커맨더 명령 처리
- C# Bot 컨트롤러 30Hz 루프
- 개별 봇 FSM/이동 업데이트
- 히트스캔 총기 및 라그 보상
- 점수 계산 및 이벤트 시스템

### 6️⃣ HYBRID_SYNCHRONIZATION.md
**목적**: 다중 서버 동기화 전략
**주요 섹션**:
- 클라이언트 예측 (위치 보간, 오차 보정)
- 라그 보상 (과거 위치 히트스캔)
- AI 봇 상태 동기화 (30Hz 업데이트)
- RTS 커맨더 뷰 동기화 (10Hz 보간)
- 재연결 처리 및 상태 복구
- 타임아웃 정책
- 이벤트 기반 동기화
- 거리 기반 업데이트 빈도 조정

### 7️⃣ HYBRID_BALANCING.md
**목적**: 게임 밸런싱 및 튜닝
**주요 섹션**:
- 난이도 시스템 (Easy/Normal/Hard/Extreme)
- 정확도 곡선 함수
- 5개 무기 DPS 비교
- FPS 스쿼드 능력 쿨다운
- AI 봇 프로필 (정확도, 반응시간, 용감함)
- 동적 난이도 조정
- 맵 밸런싱
- 모니터링 및 조정 주기

### 8️⃣ HYBRID_DATABASE_EXTENSION.md
**목적**: 데이터 저장 및 분석
**주요 섹션**:
- MySQL 테이블 설계
- 게임 세션 기록
- FPS 플레이어 통계 (K/D, 명중률)
- RTS 커맨더 통계 (유닛 관리, 전술)
- Bot 성능 분석
- Redis 캐싱 전략
- 쿼리 예제 및 분석
- 데이터 마이그레이션 및 백업

### 9️⃣ BOT_CONTROLLER_IMPLEMENTATION.md
**목적**: C# 봇 컨트롤러 구현 가이드
**주요 섹션**:
- .NET 8.0 프로젝트 구조
- NuGet 패키지 설정
- BotControllerServer 메인 클래스
- BotAgent 및 FSM 구현
- AccuracySystem 및 SuppressionSystem
- LiteNetLib 네트워킹
- 패킷 직렬화/역직렬화
- Docker 배포

### 🔟 HYBRID_TESTING.md
**목적**: 종합 테스트 계획
**주요 섹션**:
- 단위 테스트 (FSM, 명중률, 제압)
- 통합 테스트 (네트워크, 동기화)
- 성능 테스트 (100 봇, CPU/메모리)
- 확장성 테스트 (50/100/150/200 봇)
- 밸런스 테스트 (승률, 무기 균형)
- E2E 테스트 (완전한 게임)
- CI/CD 자동화

---

## 🔗 문서 간 참조 관계

```
README.md
├── HYBRID_GAME_SPECIFICATION.md (게임 규칙)
├── HYBRID_NETWORK_ARCHITECTURE.md (서버 구조)
│   ├── HYBRID_NETWORK_PACKETS.md (패킷)
│   └── HYBRID_SYNCHRONIZATION.md (동기화)
├── BOT_AI_SYSTEM.md (AI 설계)
│   └── HYBRID_GAME_LOGIC.md (구현)
│       └── BOT_CONTROLLER_IMPLEMENTATION.md (C# 코드)
├── HYBRID_BALANCING.md (밸런싱)
├── HYBRID_DATABASE_EXTENSION.md (저장소)
└── HYBRID_TESTING.md (테스트)
```

---

## 📋 체크리스트: 문서 완성도

```
✅ README.md - 프로젝트 개요
✅ HYBRID_GAME_SPECIFICATION.md - 12개 섹션, 800+ 라인
✅ HYBRID_NETWORK_ARCHITECTURE.md - 8개 섹션, C++/C# 코드 포함
✅ HYBRID_NETWORK_PACKETS.md - 9개 섹션, 바이너리 포맷 정의
✅ BOT_AI_SYSTEM.md - 9개 섹션, FSM 상태 코드 포함
✅ HYBRID_GAME_LOGIC.md - 7개 섹션, 완전한 게임 루프
✅ HYBRID_SYNCHRONIZATION.md - 9개 섹션, 예측/라그보상 알고리즘
✅ HYBRID_BALANCING.md - 10개 섹션, 밸런싱 공식 포함
✅ HYBRID_DATABASE_EXTENSION.md - 10개 섹션, MySQL 스키마
✅ BOT_CONTROLLER_IMPLEMENTATION.md - 7개 섹션, 프로젝트 구조
✅ HYBRID_TESTING.md - 9개 섹션, 테스트 코드 예제

총 11개 문서 (README + 10개 전문 문서)
총 라인 수: 10,000+ 줄
코드 예제: 100+ 개
다이어그램: 30+ 개
```

---

## 🎯 사용 방법

### 개발 시작하기
1. **README.md** 읽기 (5분) → 프로젝트 개요 파악
2. **HYBRID_GAME_SPECIFICATION.md** 읽기 (15분) → 게임 규칙 이해
3. **HYBRID_NETWORK_ARCHITECTURE.md** 읽기 (15분) → 서버 구조 이해
4. **역할별 문서 선택**: 위의 "읽기 순서 (역할별)" 참조

### 특정 주제 빠르게 찾기
- 게임 규칙? → HYBRID_GAME_SPECIFICATION.md
- 패킷 포맷? → HYBRID_NETWORK_PACKETS.md
- AI 동작? → BOT_AI_SYSTEM.md
- 게임 로직 코드? → HYBRID_GAME_LOGIC.md
- 동기화 문제? → HYBRID_SYNCHRONIZATION.md
- 밸런싱 조정? → HYBRID_BALANCING.md
- 데이터 저장? → HYBRID_DATABASE_EXTENSION.md
- C# 코딩? → BOT_CONTROLLER_IMPLEMENTATION.md
- 테스트? → HYBRID_TESTING.md

### 문제 해결
- 네트워크 지연 → HYBRID_SYNCHRONIZATION.md의 "라그 보상"
- AI가 너무 강함/약함 → HYBRID_BALANCING.md의 "동적 난이도"
- 패킷 손실 → HYBRID_NETWORK_PACKETS.md의 "신뢰성 정책"
- 게임 루프 프레임 드롭 → HYBRID_GAME_LOGIC.md의 "게임 루프" + HYBRID_TESTING.md의 "성능 테스트"

