# FPS 게임 사양

## 게임 개요

### 장르
1인칭 슈팅 게임 (First-Person Shooter)

### 목표
- 빠른 반응과 정확한 조준을 요구하는 멀티플레이어 FPS
- 공정한 서버 권한 기반 히트 판정
- 다양한 게임 모드 지원

---

## 게임 모드

### 1. Free For All (FFA)
```
game_mode: "ffa"
players: 2-16명
duration: 10분 또는 킬 리밋
win_condition: 가장 많은 킬 달성
respawn: 즉시 (3초 대기)
```

**룰**:
- 모든 플레이어가 적
- 킬당 +1점, 자살 -1점
- 목표: 제한 시간 내 최다 킬

### 2. Team Deathmatch (TDM)
```
game_mode: "tdm"
teams: 2팀 (Red vs Blue)
players: 4-16명 (팀당 2-8명)
duration: 10분 또는 킬 리밋
win_condition: 팀 총합 킬 수
respawn: 즉시 (3초 대기)
```

**룰**:
- 팀 단위 대결
- 킬당 팀 점수 +1, 팀킬 -1
- 목표: 팀 총합 킬 먼저 달성

### 3. Capture The Flag (CTF)
```
game_mode: "ctf"
teams: 2팀
players: 6-16명
duration: 15분
win_condition: 깃발 탈취 횟수
respawn: 5초 대기
```

**룰**:
- 상대 팀 깃발을 자기 팀 기지로 가져오기
- 자기 팀 깃발이 기지에 있어야 점수 획득
- 깃발 소지자 사망 시 깃발 드롭 (30초 후 자동 귀환)

---

## 플레이어 스펙

### 기본 능력치
```json
{
  "health": 100,
  "max_health": 100,
  "move_speed": 5.0,         // m/s
  "sprint_speed": 7.5,       // m/s
  "jump_height": 1.5,        // m
  "view_height": 1.7,        // m
  "crouch_height": 0.9       // m
}
```

### 이동
- **걷기**: 5 m/s
- **달리기**: 7.5 m/s (스태미나 소모 없음)
- **점프**: 1.5m 높이
- **앉기**: 이동 속도 50% 감소, 정확도 향상

### 체력 시스템
- 최대 체력: 100
- 데미지 받으면 즉시 반영
- 힐팩 아이템으로 회복 가능 (+50 HP)
- 낙하 데미지: 5m 이상 떨어지면 (높이 - 5) * 10 데미지

---

## 무기 시스템

### 무기 카테고리

#### 1. Assault Rifle (AR)
```json
{
  "weapon_id": "ar_default",
  "type": "hitscan",
  "damage": 25,
  "fire_rate": 600,          // RPM (분당 발사 수)
  "magazine_size": 30,
  "reload_time": 2.0,        // 초
  "range": 100,              // m
  "recoil": {
    "vertical": 1.5,
    "horizontal": 0.5
  },
  "accuracy": 0.95           // 기본 명중률
}
```

**특징**: 만능형, 중거리 전투 최적

#### 2. Submachine Gun (SMG)
```json
{
  "weapon_id": "smg_default",
  "type": "hitscan",
  "damage": 18,
  "fire_rate": 900,
  "magazine_size": 25,
  "reload_time": 1.5,
  "range": 50,
  "recoil": {
    "vertical": 1.0,
    "horizontal": 1.2
  },
  "accuracy": 0.85
}
```

**특징**: 근거리 고속 연사

#### 3. Sniper Rifle (SR)
```json
{
  "weapon_id": "sniper_default",
  "type": "hitscan",
  "damage": 100,             // 헤드샷 즉사
  "fire_rate": 40,           // 볼트액션
  "magazine_size": 5,
  "reload_time": 3.0,
  "range": 300,
  "recoil": {
    "vertical": 10.0,
    "horizontal": 0.0
  },
  "accuracy": 0.99,
  "scope_zoom": 4.0          // 4배율
}
```

**특징**: 원거리 고데미지

#### 4. Shotgun (SG)
```json
{
  "weapon_id": "shotgun_default",
  "type": "hitscan_spread",
  "damage": 12,              // 펠릿당
  "pellets": 8,              // 총 8개 발사
  "fire_rate": 60,           // 펌프액션
  "magazine_size": 6,
  "reload_time": 0.5,        // 탄환당
  "range": 20,
  "spread": 5.0              // 각도
}
```

**특징**: 초근거리 고폭발력

#### 5. Rocket Launcher (RL)
```json
{
  "weapon_id": "rocket_default",
  "type": "projectile",
  "damage": 120,
  "fire_rate": 30,
  "magazine_size": 1,
  "reload_time": 2.5,
  "projectile_speed": 30,    // m/s
  "splash_radius": 5,        // m
  "splash_damage": 50        // 최대 거리에서
}
```

**특징**: 투사체 기반, 스플래시 데미지

---

## 히트 판정

### 히트 박스
```
Head:     x1.0 (크기), x2.5 (데미지 배율) - 헤드샷
Chest:    x1.0, x1.0 - 기본 데미지
Stomach:  x1.0, x0.9
Arms:     x0.5, x0.75 - 팔 맞으면 75% 데미지
Legs:     x0.7, x0.75 - 다리 맞으면 75% 데미지
```

### Hitscan vs Projectile

#### Hitscan (즉시 판정)
- 발사 즉시 Raycast로 히트 판정
- 레이저처럼 즉시 도달
- 대부분 무기 (AR, SMG, SR, SG)

#### Projectile (투사체)
- 물리 기반 발사체
- 중력, 공기 저항 적용
- 로켓, 수류탄

---

## 맵 구조

### 맵 요소

#### 스폰 포인트
```json
{
  "spawn_points": [
    {
      "id": "spawn_01",
      "position": {"x": 10, "y": 0, "z": 20},
      "rotation": {"x": 0, "y": 90, "z": 0},
      "team": "neutral"  // "neutral", "red", "blue"
    }
  ]
}
```

#### 아이템 스폰
```json
{
  "item_spawns": [
    {
      "id": "health_01",
      "type": "health_pack",
      "position": {"x": 5, "y": 0, "z": 10},
      "respawn_time": 30  // 초
    },
    {
      "id": "ammo_01",
      "type": "ammo_box",
      "position": {"x": 15, "y": 0, "z": 10},
      "respawn_time": 20
    }
  ]
}
```

### 맵 크기
- **Small**: 30m x 30m (4-8 플레이어)
- **Medium**: 60m x 60m (8-12 플레이어)
- **Large**: 100m x 100m (12-16 플레이어)

---

## 게임 흐름

### 1. 게임 시작 전 (Warmup)
```
- 대기 시간: 30초
- 플레이어 자유 이동 가능
- 킬/데스 기록 안 됨
- 최소 인원 충족 시 카운트다운 시작 (10초)
```

### 2. 게임 진행 중 (Playing)
```
- 게임 모드별 규칙 적용
- 킬/데스 기록
- 타이머 진행
- 승리 조건 체크
```

### 3. 게임 종료 (Finished)
```
- 최종 스코어보드 표시 (10초)
- MVP 선정 (최다 킬)
- 통계 MySQL에 저장
- 방 자동 종료 또는 재시작
```

---

## 통계 데이터

### 게임 종료 시 저장 (MySQL stats JSON)
```json
{
  "kills": 15,
  "deaths": 8,
  "assists": 3,
  "headshots": 5,
  "accuracy": 0.42,          // 명중률 (적중/발사)
  "damage_dealt": 2500,
  "damage_taken": 1800,
  "distance_traveled": 1200, // m
  "weapon_stats": {
    "ar_default": {
      "kills": 10,
      "shots": 250,
      "hits": 105
    },
    "sniper_default": {
      "kills": 5,
      "shots": 20,
      "hits": 8
    }
  },
  "game_mode": "ffa",
  "map": "map_industrial",
  "duration": 600            // 초
}
```

---

## 서버 설정

### 틱레이트
```
tick_rate: 64 Hz (15.625ms)
network_update_rate: 64 Hz (UDP)
physics_update_rate: 64 Hz
```

### 타임아웃
```
player_timeout: 10초 (연결 끊김 판정)
afk_timeout: 60초 (AFK 킥)
```

### 제한
```
max_ping: 200ms (초과 시 킥)
max_packet_loss: 10%
```

---

## 치트 방지

### 서버 검증
1. **이동 속도**: 최대 속도 초과 감지
2. **히트 판정**: 서버에서만 히트 계산
3. **발사 속도**: 무기별 RPM 초과 감지
4. **탄약**: 서버에서 탄약 카운트 관리
5. **시야각**: 비정상적 시야 감지

### 패킷 검증
- 모든 클라이언트 입력 타임스탬프 검증
- 비정상적 패킷 순서 감지
- Rate limiting 적용

---

## 최적화 전략

### 네트워크
- **델타 압축**: 변경된 값만 전송
- **우선순위**: 가까운 플레이어 우선 업데이트
- **관심 영역**: 시야 밖 플레이어 업데이트 빈도 감소

### 메모리
- 오브젝트 풀링 (발사체, 이펙트)
- 메모리 재사용

### CPU
- 공간 분할 (Spatial Partitioning)
- 거리 기반 LOD
