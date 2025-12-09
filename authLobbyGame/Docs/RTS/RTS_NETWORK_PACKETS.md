# RTS 네트워크 패킷 명세서 (RTS Network Packets Specification)

## 1. 패킷 구조 개요

### 1.1 기본 패킷 헤더
```
[1 byte: Protocol Version]
[1 byte: Packet Type]
[2 bytes: Payload Length]
[4 bytes: Sequence Number]
[8 bytes: Timestamp]
[Variable: Payload]
[4 bytes: Checksum]

총 최소 크기: 20 bytes
```

### 1.2 패킷 타입 정의
```csharp
public enum RtsPacketType : byte
{
    // 게임 초기화 (0x10~0x1F)
    GameInit = 0x10,
    MapData = 0x11,
    PlayerInfo = 0x12,
    
    // 명령 (0x20~0x2F)
    CommandBatch = 0x20,
    UnitCommand = 0x21,
    BuildCommand = 0x22,
    ResearchCommand = 0x23,
    
    // 게임 상태 동기화 (0x30~0x3F)
    StateDelta = 0x30,
    FullStateSync = 0x31,
    UnitUpdate = 0x32,
    BuildingUpdate = 0x33,
    ResourceUpdate = 0x34,
    
    // 포그 오브 워 (0x40~0x4F)
    FogOfWarUpdate = 0x40,
    VisibilityMap = 0x41,
    
    // 게임 이벤트 (0x50~0x5F)
    UnitDeath = 0x50,
    BuildingDestroyed = 0x51,
    Victory = 0x52,
    Defeat = 0x53,
    
    // 채팅 (0x60~0x6F)
    ChatMessage = 0x60,
    
    // 제어 (0x70~0x7F)
    Ping = 0x70,
    Pong = 0x71,
    Disconnect = 0x72
}
```

---

## 2. 게임 초기화 패킷

### 2.1 GameInit 패킷 (0x10)
게임 시작 시 클라이언트에 게임 기본 정보 전송

```json
{
  "packet_type": 0x10,
  "game_id": "game_12345",
  "players": [
    {
      "player_id": 1,
      "name": "Player1",
      "color": 0xFF0000,
      "start_position": {"x": 100, "y": 100},
      "initial_resources": {
        "minerals": 500,
        "gas": 100,
        "food": 15
      }
    },
    {
      "player_id": 2,
      "name": "Player2",
      "color": 0x0000FF,
      "start_position": {"x": 400, "y": 400},
      "initial_resources": {
        "minerals": 500,
        "gas": 100,
        "food": 15
      }
    }
  ],
  "game_mode": "1v1",
  "map_id": "map_001",
  "game_speed": 1.0,
  "timestamp": 1702154400000
}
```

**재직렬화 크기**: ~300 bytes

### 2.2 MapData 패킷 (0x11)
맵 정보 및 타일 데이터 전송 (여러 패킷으로 분할 가능)

```json
{
  "packet_type": 0x11,
  "map_id": "map_001",
  "width": 256,
  "height": 256,
  "tile_size": 32,
  "chunk_index": 0,
  "total_chunks": 16,
  "terrain_data": "base64_encoded_tile_data",
  "resource_locations": [
    {
      "type": "mineral",
      "x": 150,
      "y": 150,
      "amount": 1500
    },
    {
      "type": "gas",
      "x": 200,
      "y": 200,
      "amount": 300
    }
  ],
  "obstacles": [
    {"x": 100, "y": 100, "width": 50, "height": 50}
  ]
}
```

**최대 크기**: ~64KB (청크 단위로 분할)

### 2.3 PlayerInfo 패킷 (0x12)
플레이어 정보 및 통계 동기화

```json
{
  "packet_type": 0x12,
  "player_id": 1,
  "name": "Player1",
  "color": 0xFF0000,
  "current_resources": {
    "minerals": 450,
    "gas": 95,
    "food": 12,
    "current_population": 3,
    "max_population": 15
  },
  "statistics": {
    "units_killed": 5,
    "buildings_destroyed": 1,
    "resources_gathered": 1200,
    "damage_dealt": 350,
    "damage_taken": 100
  }
}
```

**크기**: ~200 bytes

---

## 3. 명령 패킷

### 3.1 CommandBatch 패킷 (0x20)
여러 명령을 한 번에 전송하여 대역폭 절감

```json
{
  "packet_type": 0x20,
  "player_id": 1,
  "batch_timestamp": 1702154401000,
  "sequence_number": 100,
  "commands": [
    {
      "command_type": "move",
      "unit_ids": [1, 2, 3],
      "target_x": 200,
      "target_y": 200
    },
    {
      "command_type": "attack",
      "unit_ids": [4, 5],
      "target_unit_id": 10
    },
    {
      "command_type": "gather",
      "unit_ids": [6, 7, 8],
      "resource_type": "mineral"
    }
  ]
}
```

**크기**: ~150 bytes (명령 3~5개 포함)

### 3.2 UnitCommand 패킷 (0x21)
단일 유닛 명령 (즉각적인 처리 필요 시)

```json
{
  "packet_type": 0x21,
  "player_id": 1,
  "unit_id": 42,
  "command_type": "move",
  "target": {"x": 250, "y": 250},
  "formation": "spread",
  "sequence_number": 101,
  "timestamp": 1702154401100
}
```

**명령 종류**:
- `move`: 이동 명령
- `attack`: 공격 명령
- `stop`: 정지 명령
- `gather`: 자원 채취 명령
- `patrol`: 순찰 명령
- `hold_position`: 위치 유지 명령

**크기**: ~80 bytes

### 3.3 BuildCommand 패킷 (0x22)
건설 명령

```json
{
  "packet_type": 0x22,
  "player_id": 1,
  "building_type": "barracks",
  "position": {"x": 150, "y": 200},
  "worker_id": 5,
  "sequence_number": 102,
  "timestamp": 1702154401200
}
```

**크기**: ~70 bytes

### 3.4 ResearchCommand 패킷 (0x23)
기술 연구 명령

```json
{
  "packet_type": 0x23,
  "player_id": 1,
  "building_id": 10,
  "research_type": "weapon_upgrade",
  "upgrade_level": 1,
  "sequence_number": 103,
  "timestamp": 1702154401300
}
```

**크기**: ~60 bytes

---

## 4. 게임 상태 동기화 패킷

### 4.1 StateDelta 패킷 (0x30) - 부분 동기화
변경된 데이터만 전송하여 대역폭 최적화

```json
{
  "packet_type": 0x30,
  "tick_number": 150,
  "delta_time_ms": 100,
  "timestamp": 1702154401500,
  "changes": {
    "unit_updates": [
      {
        "unit_id": 1,
        "x": 250.5,
        "y": 180.3,
        "health": 45,
        "state": "moving",
        "target_unit_id": null
      },
      {
        "unit_id": 2,
        "x": 252.0,
        "y": 182.1,
        "health": 50,
        "state": "attacking",
        "target_unit_id": 10
      }
    ],
    "building_updates": [
      {
        "building_id": 10,
        "health": 200,
        "construction_progress": 0.75
      }
    ],
    "resource_updates": [
      {
        "player_id": 1,
        "minerals": 650,
        "gas": 150,
        "food": 14,
        "current_population": 8,
        "max_population": 25
      }
    ],
    "dead_unit_ids": [15, 16],
    "dead_building_ids": []
  }
}
```

**일반적인 크기**: 200~500 bytes (게임 상태에 따라)

### 4.2 FullStateSync 패킷 (0x31) - 전체 동기화
클라이언트 재연결 또는 심각한 동기화 오류 시 전체 상태 전송

```json
{
  "packet_type": 0x31,
  "tick_number": 150,
  "timestamp": 1702154401500,
  "all_units": [
    {
      "unit_id": 1,
      "player_id": 1,
      "type": "worker",
      "x": 250.5,
      "y": 180.3,
      "health": 45,
      "max_health": 50,
      "state": "moving",
      "target_x": 300,
      "target_y": 200,
      "target_unit_id": null
    }
  ],
  "all_buildings": [
    {
      "building_id": 10,
      "player_id": 1,
      "type": "barracks",
      "x": 100,
      "y": 100,
      "health": 200,
      "max_health": 300,
      "construction_progress": 1.0,
      "current_research": null
    }
  ],
  "all_players": [
    {
      "player_id": 1,
      "minerals": 650,
      "gas": 150,
      "food": 14,
      "current_population": 8,
      "max_population": 25,
      "is_defeated": false
    }
  ]
}
```

**크기**: 10KB~50KB (게임 규모에 따라)

### 4.3 UnitUpdate 패킷 (0x32)
특정 유닛 업데이트 (중요한 변화)

```json
{
  "packet_type": 0x32,
  "unit_id": 1,
  "player_id": 1,
  "position": {"x": 250.5, "y": 180.3},
  "velocity": {"x": 2.0, "y": 0.0},
  "health": 45,
  "state": "moving",
  "timestamp": 1702154401600
}
```

**크기**: ~70 bytes

### 4.4 BuildingUpdate 패킷 (0x33)
특정 건물 업데이트

```json
{
  "packet_type": 0x33,
  "building_id": 10,
  "player_id": 1,
  "health": 200,
  "max_health": 300,
  "construction_progress": 0.85,
  "production_queue": [
    {"unit_type": "infantry", "progress": 0.3},
    {"unit_type": "archer", "progress": 0.0}
  ],
  "timestamp": 1702154401700
}
```

**크기**: ~100 bytes

### 4.5 ResourceUpdate 패킷 (0x34)
플레이어 자원 업데이트

```json
{
  "packet_type": 0x34,
  "player_id": 1,
  "minerals": 650,
  "gas": 150,
  "food": 14,
  "current_population": 8,
  "max_population": 25,
  "timestamp": 1702154401800
}
```

**크기**: ~50 bytes

---

## 5. 포그 오브 워 패킷

### 5.1 FogOfWarUpdate 패킷 (0x40)
부분 포그 업데이트

```json
{
  "packet_type": 0x40,
  "tick_number": 150,
  "visible_units": [1, 2, 3, 4],
  "visible_buildings": [10, 11],
  "hidden_units": [15, 16],
  "visibility_changes": [
    {
      "position": {"x": 200, "y": 200},
      "visibility_radius": 15,
      "state": "visible"
    }
  ],
  "timestamp": 1702154401900
}
```

**크기**: ~150 bytes

### 5.2 VisibilityMap 패킷 (0x41)
전체 가시성 맵 업데이트 (큰 패킷, 청크로 분할)

```json
{
  "packet_type": 0x41,
  "chunk_index": 0,
  "total_chunks": 4,
  "width": 128,
  "height": 128,
  "visibility_data": "base64_encoded_bitfield",
  "timestamp": 1702154402000
}
```

**설명**:
- 각 타일당 2비트 (0: unexplored, 1: explored, 2: visible)
- 256x256 맵 = 128KB (압축 시 ~32KB)
- 청크 단위로 분할 전송

**크기**: ~16KB per chunk

---

## 6. 게임 이벤트 패킷

### 6.1 UnitDeath 패킷 (0x50)
유닛 사망 이벤트

```json
{
  "packet_type": 0x50,
  "unit_id": 15,
  "player_id": 1,
  "death_position": {"x": 200, "y": 150},
  "killer_unit_id": 5,
  "killer_player_id": 2,
  "death_type": "combat",
  "timestamp": 1702154402100
}
```

**death_type**: `combat`, `building_fire`, `environment`, `starvation`

**크기**: ~60 bytes

### 6.2 BuildingDestroyed 패킷 (0x51)
건물 파괴 이벤트

```json
{
  "packet_type": 0x51,
  "building_id": 20,
  "player_id": 1,
  "building_type": "barracks",
  "destruction_position": {"x": 100, "y": 100},
  "destroyer_player_id": 2,
  "timestamp": 1702154402200
}
```

**크기**: ~60 bytes

### 6.3 Victory 패킷 (0x52)
게임 승리 이벤트

```json
{
  "packet_type": 0x52,
  "winner_player_id": 1,
  "winner_name": "Player1",
  "win_reason": "base_destroyed",
  "game_duration_seconds": 1250,
  "final_statistics": {
    "units_killed": 25,
    "buildings_destroyed": 5,
    "resources_gathered": 5000,
    "damage_dealt": 2500,
    "damage_taken": 800
  },
  "timestamp": 1702154402300
}
```

**win_reason**: `base_destroyed`, `score_goal`, `opponent_surrender`, `time_limit`

**크기**: ~200 bytes

### 6.4 Defeat 패킷 (0x53)
게임 패배 이벤트

```json
{
  "packet_type": 0x53,
  "defeated_player_id": 1,
  "defeat_reason": "base_destroyed",
  "conqueror_player_id": 2,
  "game_duration_seconds": 1250,
  "timestamp": 1702154402400
}
```

**크기**: ~80 bytes

---

## 7. 채팅 패킷

### 7.1 ChatMessage 패킷 (0x60)
플레이어 채팅 메시지

```json
{
  "packet_type": 0x60,
  "player_id": 1,
  "player_name": "Player1",
  "message": "Good game!",
  "message_type": "all_chat",
  "timestamp": 1702154402500
}
```

**message_type**: `all_chat`, `team_chat`, `system_message`

**크기**: ~100 bytes

---

## 8. 제어 패킷

### 8.1 Ping 패킷 (0x70)
네트워크 지연 측정

```json
{
  "packet_type": 0x70,
  "sequence_number": 1000,
  "client_timestamp": 1702154402600
}
```

**크기**: ~20 bytes

### 8.2 Pong 패킷 (0x71)
Ping에 대한 응답

```json
{
  "packet_type": 0x71,
  "sequence_number": 1000,
  "client_timestamp": 1702154402600,
  "server_timestamp": 1702154402605
}
```

**크기**: ~30 bytes

**RTT 계산**: `server_timestamp - client_timestamp`

### 8.3 Disconnect 패킷 (0x72)
연결 종료 알림

```json
{
  "packet_type": 0x72,
  "player_id": 1,
  "reason": "player_quit",
  "timestamp": 1702154402700
}
```

**reason**: `player_quit`, `timeout`, `network_error`, `server_shutdown`

**크기**: ~50 bytes

---

## 9. 전송 최적화 전략

### 9.1 전송 빈도
```
높은 우선순위 (매 틱마다, 100ms):
- StateDelta (부분 동기화)
- FogOfWarUpdate

중간 우선순위 (200ms마다):
- ResourceUpdate
- BuildingUpdate

낮은 우선순위 (500ms마다):
- DetailedUnitInfo
- Leaderboard
```

### 9.2 대역폭 계산
```
1v1 게임 기준:
- CommandBatch: 150 bytes × 10/sec = 1.5 KB/s
- StateDelta: 300 bytes × 10/sec = 3 KB/s
- FogOfWar: 150 bytes × 10/sec = 1.5 KB/s
- Events: 100 bytes × variable = 1~2 KB/s
- Ping/Pong: 50 bytes × 1/sec = 0.05 KB/s

총 대역폭: ~7.5 KB/s (업로드/다운로드)
월별 사용량: ~32 GB (1v1 게임 100시간 기준)
```

### 9.3 압축 기법
```
적용 시점:
- 패킷 크기 > 256 bytes: ZSTD 압축
- 맵 데이터: 항상 압축
- 포그 오브 워: 항상 압축

압축률:
- 게임 상태 데이터: 40~60% 압축 가능
- 맵 데이터: 70~80% 압축 가능
- 포그 데이터: 80~90% 압축 가능
```

### 9.4 델타 동기화
```
변경 감지:
1. Position: ±1.0 이상 변경 시만 전송
2. Health: ±5 이상 변경 시만 전송
3. State: 상태 변경 시 항상 전송
4. Target: 목표 변경 시 항상 전송

예상 절감:
- 기존: 300 bytes × 10/sec = 3 KB/s
- 델타 적용: 100 bytes × 10/sec = 1 KB/s (66% 절감)
```

---

## 10. 에러 처리

### 10.1 패킷 검증
```csharp
public bool ValidatePacket(RtsPacket packet)
{
    // 1. 크기 검증
    if (packet.Length > MAX_PACKET_SIZE)
        return false;
    
    // 2. 체크섬 검증
    if (!VerifyChecksum(packet))
        return false;
    
    // 3. 시퀀스 번호 검증 (재전송 방지)
    if (!IsValidSequenceNumber(packet))
        return false;
    
    // 4. 타임스탬프 검증 (시간 왜곡 방지)
    if (!IsValidTimestamp(packet))
        return false;
    
    return true;
}
```

### 10.2 재전송 로직
```
신뢰성 필요 패킷:
- CommandBatch
- BuildCommand
- ResearchCommand

재전송 정책:
- 재전송 대기: 500ms
- 최대 재전송: 3회
- 최종 실패: 연결 종료
```

---

## 11. 보안 고려사항

### 11.1 명령 검증
```
서버에서 모든 클라이언트 명령 검증:
1. 플레이어 권한 확인
2. 자원 소유권 확인
3. 자원 충분성 확인
4. 지도 경계 확인
5. 지형 유효성 확인

예시:
- 다른 플레이어의 유닛 제어 불가
- 보유하지 않은 자원 소비 불가
- 맵 범위 외 건설 불가
```

### 11.2 클라이언트 예측 vs 서버 보정
```
클라이언트:
1. 명령 즉시 실행 (낮은 지연)
2. 로컬 상태 업데이트

서버:
1. 명령 검증
2. 게임 상태 업데이트
3. 보정 데이터 클라이언트로 전송

클라이언트:
1. 서버 보정 데이터 수신
2. 로컬 상태와 비교
3. 불일치 시 보정 적용
```

