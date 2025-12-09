# FPS/RTS 하이브리드 게임 기술 문서

이 폴더는 4인 FPS 스쿼드 vs 1인 RTS 커맨더 비대칭 멀티플레이어 게임의 기술 명세서를 포함합니다.

## 📋 문서 목록

### 1. [HYBRID_GAME_SPECIFICATION.md](HYBRID_GAME_SPECIFICATION.md)
하이브리드 게임의 통합 명세서
- 게임 규칙 및 승리 조건
- FPS 스쿼드 플레이 (4인)
- RTS 커맨더 플레이 (1인)
- 비대칭 밸런싱 메커니즘

### 2. [HYBRID_NETWORK_ARCHITECTURE.md](HYBRID_NETWORK_ARCHITECTURE.md)
네트워크 아키텍처 및 통신 프로토콜
- 언리얼 데디케이티드 서버
- C# Bot Controller 서버
- LiteNetLib 통신
- 데이터 흐름 및 동기화

### 3. [HYBRID_NETWORK_PACKETS.md](HYBRID_NETWORK_PACKETS.md)
하이브리드 게임 전용 패킷 정의
- FPS ↔ 서버 패킷 (Unreal Replication)
- RTS ↔ 서버 패킷 (LiteNetLib)
- Bot 상태 동기화 패킷
- 커맨드 패킷 (명령)

### 4. [BOT_AI_SYSTEM.md](BOT_AI_SYSTEM.md)
AI Bot 시스템 (C#)
- 타르코프 스캐브 스타일 AI
- FSM 상태 머신
- 명중률 시스템
- 제압 시스템
- 개별 Bot 인스턴스 관리

### 5. [HYBRID_GAME_LOGIC.md](HYBRID_GAME_LOGIC.md)
하이브리드 게임 로직 구현
- FPS 클라이언트 로직
- RTS 클라이언트 로직
- 언리얼 서버 로직
- C# Bot 컨트롤러 로직

### 6. [HYBRID_SYNCHRONIZATION.md](HYBRID_SYNCHRONIZATION.md)
멀티 서버 동기화 전략
- 언리얼 ↔ C# 서버 동기화
- FPS 플레이어 동기화
- Bot 상태 동기화
- RTS 커맨더 뷰 동기화
- 지연 보정 및 클라이언트 예측

### 7. [HYBRID_DATABASE_EXTENSION.md](HYBRID_DATABASE_EXTENSION.md)
하이브리드 게임 데이터베이스 확장
- 게임 세션 저장
- 플레이어 통계 (FPS/RTS 분리)
- 게임 리플레이 데이터
- Bot AI 학습 데이터 (선택)

### 8. [HYBRID_BALANCING.md](HYBRID_BALANCING.md)
비대칭 게임 밸런싱
- FPS 스쿼드 vs Bot 군단 밸런싱
- 어려움 레벨 (Easy/Normal/Hard)
- 플레이타임 및 승률 목표
- 튜닝 가이드

### 9. [BOT_CONTROLLER_IMPLEMENTATION.md](BOT_CONTROLLER_IMPLEMENTATION.md)
C# Bot Controller 구현 가이드
- 프로젝트 구조
- LiteNetLib 설정
- Bot AI 루프
- 언리얼 연동
- 배포 및 운영

### 10. [HYBRID_TESTING.md](HYBRID_TESTING.md)
테스트 및 검증 계획
- 기능 테스트
- 성능 테스트
- 부하 테스트
- 동기화 테스트
- 밸런싱 테스트

---

## 🎮 게임 개요

### 게임 컨셉
**4인 FPS 스쿼드 vs 1인 RTS 커맨더 (100개 AI 유닛)**

- **FPS 측 (4명)**: 1인칭/3인칭 전투, 전술적 팀플레이
- **RTS 측 (1명)**: 탑다운 뷰, 100개 AI 유닛 지휘 (Company of Heroes 스타일)
- **핵심 경험**: 소수 정예 vs 물량, 비대칭 전략 전투

### 승리 조건

**FPS 스쿼드 승리** (3가지 중 1개):
1. 모든 AI 유닛 섬멸 (100/100 처치)
2. 특수 미션 달성 (선택적)
3. 제한 시간(20분) 생존

**RTS 커맨더 승리**:
- 4인 스쿼드 전원 제거

---

## 🏗️ 아키텍처

### 서버 구성
```
┌─────────────────────────────────────────────────────────┐
│                   언리얼 데디케이티드 서버                 │
│  (100v100 GameMode - 4인 팀 + 100개 Bot 팀)            │
├─────────────────────────────────────────────────────────┤
│  • FPS 플레이어 입력 처리 (60Hz)                        │
│  • Bot 물리/애니메이션 업데이트 (60Hz)                  │
│  • 전투 판정 및 데미지 계산                              │
│  • 게임 상태 관리                                       │
└─────────────────────────────────────────────────────────┘
            ↕ LiteNetLib (30Hz 동기화)
┌─────────────────────────────────────────────────────────┐
│               C# Bot Controller 서버                     │
│           (.NET 8.0 + FSM/BT AI System)                │
├─────────────────────────────────────────────────────────┤
│  • 100개 개별 Bot AI 루프 (FSM 기반)                   │
│  • Bot 명중률 시스템                                    │
│  • Bot 제압 상태 관리                                   │
│  • RTS 커맨더 명령 처리                                 │
│  • 전술 AI (엄폐, 측면 공격)                            │
└─────────────────────────────────────────────────────────┘
```

### 클라이언트 구성
```
FPS 클라이언트 × 4 (언리얼 엔진 5)
├─ 1인칭 슈터 UI
├─ 무기/체력 표시
└─ 팀 동료 위치 표시

RTS 클라이언트 × 1 (언리얼 엔진 5)
├─ 탑다운 카메라
├─ 유닛 선택/이동 명령
├─ Bot 상태 모니터링
└─ 미니맵
```

---

## 📊 네트워크 데이터량

| 구간 | 방향 | 패킷 크기 | 빈도 | 대역폭 |
|-----|------|---------|------|--------|
| FPS ↔ Unreal | 양방향 | 20B | 60Hz | 4.8 KB/s |
| RTS → C# Bot | 양방향 | 50B | Event | 0.15 KB/s |
| Unreal ↔ C# Bot | 양방향 | 17B × 100 | 30Hz | 102 KB/s |
| **총 대역폭** | - | - | - | **~107 KB/s** |

---

## 🔄 개발 단계

### Phase 1: 기본 인프라 (4주)
- [ ] 언리얼 100v100 GameMode 설정
- [ ] Bot PlayerController 기본 이동
- [ ] C# 서버 LiteNetLib 연결
- [ ] 언리얼 ↔ C# 통신 프로토콜

**마일스톤**: 4 FPS vs 30 Bot 기본 동작

### Phase 2: AI 및 RTS 통합 (4주)
- [ ] 100개 유닛 관리 및 FSM
- [ ] RTS UI (드래그 선택, 이동 명령만)
- [ ] 경로 찾기 (A*)
- [ ] C# → Unreal Bot 입력 전송

**마일스톤**: RTS 커맨더가 유닛 조작 가능

### Phase 3: AI 전투 시스템 (4주)
- [ ] 엄폐 탐색 AI
- [ ] 명중률 시스템
- [ ] 제압 시스템
- [ ] 스캐브 AI 행동

**마일스톤**: 타르코프 스캐브 스타일 AI 완성

### Phase 4: 폴리싱 및 최적화
- [ ] 게임 밸런싱
- [ ] 성능 최적화
- [ ] UI/UX 개선
- [ ] 사운드/이펙트

---

## 🎯 주요 특징

### 1. 비대칭 게임플레이
- **FPS**: 협력하는 소수 정예 팀
- **RTS**: 대량 AI 유닛을 지휘하는 커맨더

### 2. 독특한 AI 시스템
- 타르코프 스캐브 스타일 반응형 AI
- 개별 Bot별 명중률 시스템
- 제압 상태 관리
- 자율 측면 공격/후퇴 AI

### 3. 효율적인 네트워크 구조
- 언리얼 Replication Graph로 FPS 최적화
- C# 경량 서버로 100개 Bot 관리
- 총 ~107 KB/s의 매우 낮은 대역폭

### 4. 규모 확장성
- 여러 게임 세션 동시 운영 가능
- Bot Controller는 상태비저장(stateless)
- 언리얼 클러스터로 확장 가능

---

## 📁 연관 문서

### 상위 프레임워크
- [Docs/ARCHITECTURE.md](../ARCHITECTURE.md) - 전체 아키텍처
- [Docs/NETWORK_PROTOCOL.md](../NETWORK_PROTOCOL.md) - 기본 프로토콜

### FPS 게임
- [Docs/FPS/README.md](../FPS/README.md) - FPS 게임 가이드

### RTS 게임
- [Docs/RTS/README.md](../RTS/README.md) - RTS 게임 가이드

---

## 🛠️ 기술 스택

| 레이어 | 기술 | 비고 |
|--------|------|------|
| 게임 클라이언트 | Unreal Engine 5 | C++ + Blueprint |
| 게임 서버 | Unreal Dedicated Server | C++ |
| Bot Controller | .NET 8.0 | C# + LiteNetLib |
| 네트워크 | Unreal Replication + LiteNetLib | 하이브리드 |
| 데이터베이스 | MySQL 8.0+ | 게임 결과 저장 |
| 캐싱 | Redis 7.0+ | 세션/상태 캐싱 |

---

## 📞 문의 및 피드백

각 문서에서 구체적인 구현 가이드와 코드 샘플을 찾을 수 있습니다.

