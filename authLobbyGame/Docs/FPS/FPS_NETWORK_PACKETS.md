# FPS 네트워크 패킷 정의

## 패킷 범위

FPS 게임 전용 패킷은 `0x4000 - 0x4FFF` 범위를 사용합니다.

---

## TCP 패킷 (중요 이벤트)

### 0x4001: PlayerSpawn
플레이어 스폰 요청 및 알림

#### 요청 (Client → Server)
```
[Header: 10 bytes]
[Body: 0 bytes]  // 빈 요청
```

#### 응답 (Server → Client)
```
[Header: 10 bytes]
[Body]
  player_id: string        // 2 + N bytes
  spawn_position: Vector3  // 12 bytes
  spawn_rotation: Vector3  // 12 bytes (Euler angles)
  team: byte              // 1 byte (0=neutral, 1=red, 2=blue)
  health: int             // 4 bytes
  weapon_id: string       // 2 + N bytes
```

**슈도 코드**:
```pseudocode
function serialize_player_spawn_response(response):
    serialize_string(response.player_id)
    serialize_vector3(response.spawn_position)
    serialize_vector3(response.spawn_rotation)
    serialize_byte(response.team)
    serialize_int(response.health)
    serialize_string(response.weapon_id)
```

---

### 0x4002: PlayerDeath
플레이어 사망 알림

#### 서버 → 모든 클라이언트
```
[Header: 10 bytes]
[Body]
  victim_id: string        // 2 + N bytes
  killer_id: string        // 2 + N bytes (자살 시 빈 문자열)
  weapon_id: string        // 2 + N bytes
  is_headshot: bool       // 1 byte
  death_position: Vector3 // 12 bytes
```

---

### 0x4003: WeaponSwitch
무기 교체

#### 클라이언트 → 서버
```
[Header: 10 bytes]
[Body]
  weapon_id: string  // 2 + N bytes
```

#### 서버 → 모든 클라이언트 (브로드캐스트)
```
[Header: 10 bytes]
[Body]
  player_id: string  // 2 + N bytes
  weapon_id: string  // 2 + N bytes
```

---

### 0x4004: GameStateChange
게임 상태 변경 (시작, 종료 등)

#### 서버 → 모든 클라이언트
```
[Header: 10 bytes]
[Body]
  game_state: byte     // 1 byte (0=warmup, 1=playing, 2=finished)
  time_remaining: int  // 4 bytes (초)
  scores: array        // 팀 또는 개인 점수
    - length: ushort   // 2 bytes
    - entries:
      - player_id: string  // 2 + N bytes
      - score: int         // 4 bytes
```

---

### 0x4005: ItemPickup
아이템 획득

#### 클라이언트 → 서버
```
[Header: 10 bytes]
[Body]
  item_id: string  // 2 + N bytes (맵상의 아이템 ID)
```

#### 서버 → 모든 클라이언트 (브로드캐스트)
```
[Header: 10 bytes]
[Body]
  player_id: string      // 2 + N bytes
  item_id: string        // 2 + N bytes
  item_type: byte        // 1 byte (0=health, 1=ammo, 2=armor)
  respawn_time: float    // 4 bytes (초)
```

---

### 0x4006: Scoreboard
스코어보드 업데이트

#### 서버 → 클라이언트 (요청 시 또는 주기적)
```
[Header: 10 bytes]
[Body]
  game_mode: string       // 2 + N bytes
  time_remaining: int     // 4 bytes
  player_count: ushort    // 2 bytes
  players: array
    - player_id: string
    - username: string
    - team: byte
    - kills: int
    - deaths: int
    - ping: int           // ms
```

---

## UDP 패킷 (실시간 동기화)

### 0x4101: PlayerInput
플레이어 입력 (클라이언트 → 서버)

```
[Header: 10 bytes]
[Body]
  timestamp: uint         // 4 bytes (클라이언트 시간)
  sequence: uint          // 4 bytes (입력 순서)
  movement: Vector3       // 12 bytes (x=forward, y=up, z=right)
  rotation: Vector2       // 8 bytes (pitch, yaw)
  buttons: ushort         // 2 bytes (비트 플래그)
    - bit 0: jump
    - bit 1: crouch
    - bit 2: sprint
    - bit 3: fire
    - bit 4: aim
    - bit 5: reload
    - bit 6-15: 예약
  weapon_slot: byte       // 1 byte (0-9)
```

**총 크기**: 10 + 41 = **51 bytes**

**슈도 코드**:
```pseudocode
function serialize_player_input(input):
    serialize_uint(input.timestamp)
    serialize_uint(input.sequence)
    serialize_vector3(input.movement)
    serialize_vector2(input.rotation)
    serialize_ushort(input.buttons)
    serialize_byte(input.weapon_slot)
```

---

### 0x4102: PlayerState
플레이어 상태 (서버 → 클라이언트)

```
[Header: 10 bytes]
[Body]
  player_id: string       // 2 + N bytes
  timestamp: uint         // 4 bytes (서버 시간)
  position: Vector3       // 12 bytes
  rotation: Vector2       // 8 bytes (pitch, yaw)
  velocity: Vector3       // 12 bytes
  health: byte            // 1 byte (0-100)
  armor: byte             // 1 byte (0-100)
  state_flags: byte       // 1 byte
    - bit 0: is_crouching
    - bit 1: is_sprinting
    - bit 2: is_aiming
    - bit 3: is_reloading
    - bit 4: is_alive
    - bit 5-7: 예약
  current_weapon: byte    // 1 byte (무기 슬롯)
  ammo: ushort            // 2 bytes
```

**최소 크기**: 10 + 2 + 4 + 12 + 8 + 12 + 1 + 1 + 1 + 1 + 2 = **54 bytes** (player_id 최소 크기 가정)

---

### 0x4103: WeaponFire
무기 발사

#### 클라이언트 → 서버
```
[Header: 10 bytes]
[Body]
  timestamp: uint         // 4 bytes
  weapon_id: byte         // 1 byte (슬롯)
  fire_position: Vector3  // 12 bytes
  fire_direction: Vector3 // 12 bytes (정규화)
  spread_seed: uint       // 4 bytes (랜덤 시드, 샷건용)
```

**총 크기**: 10 + 33 = **43 bytes**

#### 서버 → 모든 클라이언트 (브로드캐스트)
```
[Header: 10 bytes]
[Body]
  player_id: string       // 2 + N bytes
  weapon_id: byte         // 1 byte
  fire_position: Vector3  // 12 bytes
  fire_direction: Vector3 // 12 bytes
  hit_info: nullable      // 1 + ? bytes
    - has_hit: bool       // 1 byte
    - if has_hit:
      - hit_player_id: string    // 2 + N bytes
      - hit_position: Vector3    // 12 bytes
      - damage: int              // 4 bytes
      - is_headshot: bool        // 1 byte
```

---

### 0x4104: ProjectileSpawn
투사체 생성 (로켓, 수류탄 등)

#### 서버 → 모든 클라이언트
```
[Header: 10 bytes]
[Body]
  projectile_id: uint     // 4 bytes (서버 ID)
  projectile_type: byte   // 1 byte (0=rocket, 1=grenade)
  position: Vector3       // 12 bytes
  velocity: Vector3       // 12 bytes
  owner_id: string        // 2 + N bytes
```

---

### 0x4105: ProjectileHit
투사체 히트 또는 폭발

#### 서버 → 모든 클라이언트
```
[Header: 10 bytes]
[Body]
  projectile_id: uint     // 4 bytes
  hit_position: Vector3   // 12 bytes
  hit_normal: Vector3     // 12 bytes (반사 방향)
  splash_damage: array    // 스플래시 데미지 받은 플레이어들
    - count: byte         // 1 byte
    - entries:
      - player_id: string
      - damage: int
```

---

### 0x4106: PlayerSnapshot (압축)
여러 플레이어 상태를 하나의 패킷으로

```
[Header: 10 bytes]
[Body]
  timestamp: uint         // 4 bytes
  player_count: byte      // 1 byte (최대 255)
  players: array
    - player_id_index: byte     // 1 byte (플레이어 인덱스)
    - position_delta: Vector3   // 12 bytes (델타 압축 가능)
    - rotation_compressed: uint // 4 bytes (16비트씩 pitch/yaw)
    - flags: byte               // 1 byte (상태 플래그)
```

**플레이어당**: 1 + 12 + 4 + 1 = **18 bytes**
**16명 전체**: 10 + 4 + 1 + (18 * 16) = **303 bytes**

---

## 패킷 최적화 전략

### 1. 델타 압축
마지막으로 전송한 값과의 차이만 전송

```pseudocode
function serialize_position_delta(current, previous):
    delta_x = current.x - previous.x
    delta_y = current.y - previous.y
    delta_z = current.z - previous.z

    // 16비트 고정소수점으로 압축 (±327.67m 범위, 0.01m 정밀도)
    serialize_short(delta_x * 100)
    serialize_short(delta_y * 100)
    serialize_short(delta_z * 100)

    return 6 bytes  // 원래 12 bytes → 6 bytes
```

### 2. 회전 압축
Euler 각을 압축

```pseudocode
function serialize_rotation_compressed(pitch, yaw):
    // 각도 범위: pitch [-90, 90], yaw [-180, 180]
    // 16비트로 압축 (0.01도 정밀도)
    pitch_compressed = (pitch + 90) * 100  // 0 ~ 18000
    yaw_compressed = (yaw + 180) * 100     // 0 ~ 36000

    serialize_ushort(pitch_compressed)
    serialize_ushort(yaw_compressed)

    return 4 bytes  // 원래 8 bytes → 4 bytes
```

### 3. 관심 영역 (Interest Management)
거리 기반 업데이트 빈도

```
0-20m: 64 Hz (매 프레임)
20-50m: 32 Hz (2프레임마다)
50-100m: 16 Hz (4프레임마다)
100m+: 4 Hz (16프레임마다)
시야 밖: 2 Hz (32프레임마다)
```

---

## 패킷 흐름 예시

### 게임 시작
```
Server → All Clients: GameStateChange (state=warmup)
Server → All Clients: Scoreboard (초기 상태)
[30초 대기]
Server → All Clients: GameStateChange (state=playing)
Server → All Clients: PlayerSpawn (모든 플레이어)
```

### 플레이어 이동 및 발사
```
Client → Server (UDP): PlayerInput (64 Hz)
Server → Client (UDP): PlayerState (64 Hz)
Server → All Clients (UDP): PlayerSnapshot (32 Hz, 여러 플레이어)

Client → Server (UDP): WeaponFire
Server → All Clients (UDP): WeaponFire (히트 정보 포함)
Server → All Clients (TCP): PlayerDeath (킬 발생 시)
Server → All Clients (TCP): Scoreboard (점수 변경)
```

### 게임 종료
```
Server → All Clients: GameStateChange (state=finished)
Server → All Clients: Scoreboard (최종 순위)
Server → MySQL: 게임 기록 저장
```

---

## 패킷 우선순위

### 높음 (즉시 전송)
- PlayerInput
- WeaponFire
- PlayerDeath
- GameStateChange

### 중간 (버퍼 가능)
- PlayerState
- PlayerSnapshot
- Scoreboard

### 낮음 (최적화 가능)
- ItemPickup
- ProjectileSpawn (먼 거리)

---

## 네트워크 통계

### 대역폭 추정

#### 클라이언트 업로드 (64 Hz)
```
PlayerInput: 51 bytes * 64 = 3,264 bytes/sec
WeaponFire: 43 bytes * 10회/sec = 430 bytes/sec (평균)

총: ~3.7 KB/sec = 29.6 Kbps
```

#### 클라이언트 다운로드 (16명 게임, 64 Hz)
```
PlayerSnapshot: 303 bytes * 32 Hz = 9,696 bytes/sec
WeaponFire (타 플레이어): 43 * 15명 * 5회/sec = 3,225 bytes/sec
기타 (스코어보드, 아이템 등): ~1,000 bytes/sec

총: ~14 KB/sec = 112 Kbps
```

**16명 게임 기준 플레이어당**: 약 **140 Kbps** (업로드 + 다운로드)

---

## 패킷 보안

### 타임스탬프 검증
```csharp
public bool ValidatePacketTimestamp(uint clientTimestamp, uint serverTime)
{
    var diff = Math.Abs((int)(serverTime - clientTimestamp));

    // 500ms 이상 차이나면 거부
    if (diff > 500) return false;

    return true;
}
```

### 시퀀스 번호 검증
```csharp
public bool ValidateSequence(uint newSeq, uint lastSeq)
{
    // 시퀀스는 항상 증가해야 함 (오래된 패킷 거부)
    if (newSeq <= lastSeq) return false;

    // 급격한 점프 방지 (100 이상 차이)
    if (newSeq - lastSeq > 100) return false;

    return true;
}
```

### Rate Limiting
```csharp
// 플레이어당 패킷 제한
const int MAX_PACKETS_PER_SECOND = 128;  // 64 Hz * 2 여유
const int MAX_FIRE_RATE = 20;            // 초당 최대 20발
```
