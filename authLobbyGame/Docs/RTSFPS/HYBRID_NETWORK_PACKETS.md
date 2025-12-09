# FPS/RTS 하이브리드 네트워크 패킷 정의

## 1. 패킷 범위 할당

```
범위: 0x5000 - 0x5FFF (하이브리드 전용)

세부 분할:
• 0x5000~0x50FF: FPS ↔ Unreal 게임 패킷
• 0x5100~0x51FF: Unreal ↔ C# Bot 패킷
• 0x5200~0x52FF: RTS ↔ C# Bot 패킷
• 0x5300~0x53FF: 제어/상태 패킷
```

---

## 2. FPS ↔ Unreal 패킷 (0x5000~0x50FF)

### 0x5001: PlayerMovement
```json
{
  "packet_type": 0x5001,
  "player_id": "player_001",
  "position": {"x": 100.5, "y": 50.0, "z": 200.5},
  "rotation": {"pitch": -10.5, "yaw": 45.0, "roll": 0},
  "input_flags": 5,  // Forward(1) | Right(2) | Jump(4)
  "timestamp": 1702154401000
}
```
**크기**: ~40 bytes
**빈도**: 60Hz (FPS 클라이언트 → Unreal)

### 0x5002: WeaponFire
```json
{
  "packet_type": 0x5002,
  "player_id": "player_001",
  "weapon_id": "rifle_001",
  "fire_position": {"x": 100, "y": 180, "z": 200},
  "fire_direction": {"x": 0.5, "y": 0.2, "z": 0.8},
  "timestamp": 1702154401010
}
```
**크기**: ~35 bytes
**빈도**: On demand (발사할 때마다)

### 0x5003: PlayerDeath
```json
{
  "packet_type": 0x5003,
  "victim_id": "player_001",
  "killer_bot_id": 15,
  "death_position": {"x": 100, "y": 50, "z": 200},
  "cause": "gunfire",
  "timestamp": 1702154401100
}
```
**크기**: ~25 bytes
**빈도**: On event

---

## 3. Unreal ↔ C# Bot 패킷 (0x5100~0x51FF)

### 0x5101: BotState (Unreal → C#)
```
[1 byte: Packet Type = 0x5101]
[4 bytes: Bot ID]
[4 bytes: Position X]
[4 bytes: Position Y]
[4 bytes: Position Z]
[1 byte: Health (0-100)]
[1 byte: Suppression (0-100)]
[4 bytes: Last Attacker ID]
[1 byte: State Flags]
[1 byte: In Cover (bool)]
───────────────────────────
총 25 bytes

전송 예시:
Bot 1: (100.5, 50.0, 200.5), Health=85, Suppression=40
```

**수신 측 처리**:
```csharp
void HandleBotState(byte[] data) {
    int botID = BitConverter.ToInt32(data, 1);
    float x = BitConverter.ToSingle(data, 5);
    float y = BitConverter.ToSingle(data, 9);
    float z = BitConverter.ToSingle(data, 13);
    byte health = data[17];
    byte suppression = data[18];
    
    BotAgent bot = GetBot(botID);
    bot.Position = new Vector3(x, y, z);
    bot.Health = health;
    bot.Suppression = suppression;
}
```

**빈도**: 30Hz
**대역폭**: 25B × 100 Bots × 30Hz = 75 KB/s

### 0x5102: BotInput (C# → Unreal)
```
[1 byte: Packet Type = 0x5102]
[4 bytes: Bot ID]
[4 bytes: Move Direction X]
[4 bytes: Move Direction Y]
[4 bytes: Move Direction Z]
[2 bytes: Aim Rotation (compressed)]
[1 byte: Action Flags]
[1 byte: State]
───────────────────────────
총 22 bytes

Action Flags (1 byte):
• Bit 0: Fire
• Bit 1: Reload
• Bit 2: In Cover
• Bit 3: Sprinting
• Bit 4: Melee
```

**빈도**: 30Hz
**대역폭**: 22B × 100 Bots × 30Hz = 66 KB/s

---

## 4. RTS ↔ C# Bot 패킷 (0x5200~0x52FF)

### 0x5201: RTSCommand (RTS 클라이언트 → C#)
```
[1 byte: Packet Type = 0x5201]
[1 byte: Command Type]
[4 bytes: Selected Bot Count]
[N × 4 bytes: Selected Bot IDs]
[4 bytes: Target Position X]
[4 bytes: Target Position Y]
[4 bytes: Target Position Z]
───────────────────────────
총 17 + (N × 4) bytes

Command Type:
• 0x01: Move
• 0x02: Attack
• 0x03: Stop

예시 (3개 유닛 선택, 이동 명령):
Count=3, IDs=[1,2,3], Target=(200,0,300)
```

**빈도**: Event-based (클릭할 때)
**신뢰성**: 필수 (확인 필요)

### 0x5202: UnitStatusUpdate (C# → RTS)
```
[1 byte: Packet Type = 0x5202]
[2 bytes: Unit Count]
[For each unit:]
  [4 bytes: Bot ID]
  [4 bytes: Position X]
  [4 bytes: Position Y]
  [1 byte: Health %]
  [1 byte: State]
  ───────────────────────
  총 14 bytes/unit
```

**예시**:
```
3개 유닛:
Bot 1: Pos=(100, 50), Health=85%, State=Combat
Bot 2: Pos=(105, 52), Health=92%, State=Patrol
Bot 3: Pos=(95, 48), Health=60%, State=Healing
```

**빈도**: 10Hz
**대역폭**: 2 + (100 × 14) bytes × 10Hz = 14 KB/s

### 0x5203: CommandAck (C# → RTS)
```
[1 byte: Packet Type = 0x5203]
[1 byte: Command Type]
[4 bytes: Command Sequence]
[1 byte: Success (bool)]
```

**목적**: RTS 클라이언트에 명령 실행 확인

---

## 5. 제어/상태 패킷 (0x5300~0x53FF)

### 0x5301: Ping (FPS/RTS → Server)
```
[1 byte: Packet Type = 0x5301]
[4 bytes: Sequence Number]
[8 bytes: Client Timestamp]
```

**빈도**: 1Hz (초당 1회)

### 0x5302: Pong (Server → FPS/RTS)
```
[1 byte: Packet Type = 0x5302]
[4 bytes: Sequence Number]
[8 bytes: Client Timestamp]
[8 bytes: Server Timestamp]
```

**용도**: RTT(Round Trip Time) 측정

### 0x5303: GameState
```json
{
  "packet_type": 0x5303,
  "game_state": "in_progress",
  "time_remaining": 1200,
  "red_kills": 50,
  "blue_alive_count": 75,
  "fps_squad_alive": 4
}
```

**빈도**: 1Hz (1초마다)

---

## 6. 전체 네트워크 다이어그램

```
FPS Client (×4)
    ↓ (PlayerMovement, WeaponFire)
    ↑ (PlayerState, Sync updates)
    
Unreal Server (Port 7777)
    ↓ (BotState, 30Hz)
    ↑ (BotInput, 30Hz)
    
C# Bot Controller (Port 7779)
    ↓ (UnitStatusUpdate, 10Hz)
    ↑ (RTSCommand, Event-based)
    
RTS Client (Port 7778)
```

---

## 7. 대역폭 최적화

### 7.1 위치 압축

대신 Float (4B) 사용:
```csharp
// 원본: 12 bytes (3 floats)
Vector3 position = new Vector3(100.5f, 50.0f, 200.5f);

// 압축: 6 bytes (3 shorts, 0.1 단위 정밀도)
short x = (short)(position.X * 10);  // 1005
short y = (short)(position.Y * 10);  // 500
short z = (short)(position.Z * 10);  // 2005

// 복원
float restored_x = x / 10.0f;  // 100.5
```

### 7.2 회전 압축

3축 회전: 12 bytes → 2 bytes
```csharp
// Quaternion: 16 bytes
// Euler: 12 bytes
// 압축: Euler → 2 bytes (각도를 byte로)

byte pitch = (byte)(rotation.X / 360 * 255);
byte yaw = (byte)(rotation.Y / 360 * 255);
// Roll은 생략 (FPS 게임에서 거의 사용 안 함)
```

### 7.3 최종 패킷 크기 최적화

Before:
- BotState: 25 bytes
- BotInput: 22 bytes

After (압축):
- BotState: 15 bytes (위치 6B, 기타 9B)
- BotInput: 12 bytes (방향 6B, 회전 1B, 플래그 5B)

절감: 25+22=47B → 15+12=27B (약 43% 감소)

---

## 8. 신뢰성 정책

```
신뢰성 있음 (TCP/Reliable):
• RTSCommand (플레이어 명령)
• PlayerDeath (중요 이벤트)

신뢰성 없음 (UDP/Unreliable):
• BotState (계속 업데이트됨)
• BotInput (다음 틱에 재전송)
• PlayerMovement (보간으로 손실 보정)
```

---

## 9. 패킷 시뮬레이션

```
초 단위 대역폭 분석 (1초간):

FPS Input (60Hz):
  40B × 4 players × 60 = 9.6 KB/s

Unreal ↔ C# Bot (30Hz):
  (25+22)B × 100 bots × 30 = 141 KB/s

RTS Update (10Hz):
  (2 + 100×14)B × 10 = 14 KB/s

상태 패킷 (1Hz):
  ~100B × 1 = 0.1 KB/s

총합: ~165 KB/s (충분히 낮음)
```

