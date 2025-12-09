# 직렬화 명세서 (Serialization Specification)

## 목적
언어 및 플랫폼에 독립적인 직렬화 로직을 정의하여 C#, Unity, 다른 언어 간 통신 호환성을 보장

## 기본 원칙

### 엔디안 (Endianness)
```
모든 멀티바이트 값은 Little Endian 사용
(네트워크 바이트 오더가 아닌 Little Endian 사용 - 대부분의 현대 시스템이 Little Endian)
```

### 문자열 인코딩
```
모든 문자열은 UTF-8 인코딩 사용
```

## 기본 타입 직렬화

### 정수형

#### byte (1바이트, unsigned)
```
슈도 코드:
function serialize_byte(value):
    buffer[offset] = value
    offset += 1
    return buffer

function deserialize_byte(buffer):
    value = buffer[offset]
    offset += 1
    return value
```

#### short (2바이트, signed)
```
슈도 코드:
function serialize_short(value):
    buffer[offset + 0] = (value >> 0) & 0xFF   // LSB (하위 바이트)
    buffer[offset + 1] = (value >> 8) & 0xFF   // MSB (상위 바이트)
    offset += 2
    return buffer

function deserialize_short(buffer):
    value = buffer[offset + 0] |               // LSB
            (buffer[offset + 1] << 8)          // MSB
    offset += 2
    // 2's complement 처리
    if value >= 32768:
        value = value - 65536
    return value
```

#### ushort (2바이트, unsigned)
```
슈도 코드:
function serialize_ushort(value):
    buffer[offset + 0] = (value >> 0) & 0xFF
    buffer[offset + 1] = (value >> 8) & 0xFF
    offset += 2
    return buffer

function deserialize_ushort(buffer):
    value = buffer[offset + 0] |
            (buffer[offset + 1] << 8)
    offset += 2
    return value
```

#### int (4바이트, signed)
```
슈도 코드:
function serialize_int(value):
    buffer[offset + 0] = (value >> 0)  & 0xFF
    buffer[offset + 1] = (value >> 8)  & 0xFF
    buffer[offset + 2] = (value >> 16) & 0xFF
    buffer[offset + 3] = (value >> 24) & 0xFF
    offset += 4
    return buffer

function deserialize_int(buffer):
    value = buffer[offset + 0]       |
            (buffer[offset + 1] << 8)  |
            (buffer[offset + 2] << 16) |
            (buffer[offset + 3] << 24)
    offset += 4
    // 2's complement 처리
    if value >= 2147483648:
        value = value - 4294967296
    return value
```

#### long (8바이트, signed)
```
슈도 코드:
function serialize_long(value):
    buffer[offset + 0] = (value >> 0)  & 0xFF
    buffer[offset + 1] = (value >> 8)  & 0xFF
    buffer[offset + 2] = (value >> 16) & 0xFF
    buffer[offset + 3] = (value >> 24) & 0xFF
    buffer[offset + 4] = (value >> 32) & 0xFF
    buffer[offset + 5] = (value >> 40) & 0xFF
    buffer[offset + 6] = (value >> 48) & 0xFF
    buffer[offset + 7] = (value >> 56) & 0xFF
    offset += 8
    return buffer

function deserialize_long(buffer):
    value = buffer[offset + 0]       |
            (buffer[offset + 1] << 8)  |
            (buffer[offset + 2] << 16) |
            (buffer[offset + 3] << 24) |
            (buffer[offset + 4] << 32) |
            (buffer[offset + 5] << 40) |
            (buffer[offset + 6] << 48) |
            (buffer[offset + 7] << 56)
    offset += 8
    return value
```

### 부동소수점

#### float (4바이트, IEEE 754 single precision)
```
슈도 코드:
function serialize_float(value):
    // IEEE 754 비트 표현으로 변환
    bits = float_to_bits(value)  // 언어별 구현 필요

    // int와 동일하게 직렬화
    buffer[offset + 0] = (bits >> 0)  & 0xFF
    buffer[offset + 1] = (bits >> 8)  & 0xFF
    buffer[offset + 2] = (bits >> 16) & 0xFF
    buffer[offset + 3] = (bits >> 24) & 0xFF
    offset += 4
    return buffer

function deserialize_float(buffer):
    bits = buffer[offset + 0]       |
           (buffer[offset + 1] << 8)  |
           (buffer[offset + 2] << 16) |
           (buffer[offset + 3] << 24)
    offset += 4

    // 비트를 float로 변환
    value = bits_to_float(bits)  // 언어별 구현 필요
    return value

// C# 예시:
// float_to_bits: BitConverter.SingleToInt32Bits()
// bits_to_float: BitConverter.Int32BitsToSingle()
```

#### double (8바이트, IEEE 754 double precision)
```
슈도 코드:
function serialize_double(value):
    bits = double_to_bits(value)  // 언어별 구현 필요

    buffer[offset + 0] = (bits >> 0)  & 0xFF
    buffer[offset + 1] = (bits >> 8)  & 0xFF
    buffer[offset + 2] = (bits >> 16) & 0xFF
    buffer[offset + 3] = (bits >> 24) & 0xFF
    buffer[offset + 4] = (bits >> 32) & 0xFF
    buffer[offset + 5] = (bits >> 40) & 0xFF
    buffer[offset + 6] = (bits >> 48) & 0xFF
    buffer[offset + 7] = (bits >> 56) & 0xFF
    offset += 8
    return buffer

function deserialize_double(buffer):
    bits = buffer[offset + 0]       |
           (buffer[offset + 1] << 8)  |
           (buffer[offset + 2] << 16) |
           (buffer[offset + 3] << 24) |
           (buffer[offset + 4] << 32) |
           (buffer[offset + 5] << 40) |
           (buffer[offset + 6] << 48) |
           (buffer[offset + 7] << 56)
    offset += 8

    value = bits_to_double(bits)  // 언어별 구현 필요
    return value
```

### 불리언

#### bool (1바이트)
```
슈도 코드:
function serialize_bool(value):
    buffer[offset] = value ? 1 : 0
    offset += 1
    return buffer

function deserialize_bool(buffer):
    value = buffer[offset] != 0
    offset += 1
    return value
```

### 문자열

#### string (가변 길이)
```
슈도 코드:
function serialize_string(value):
    // UTF-8 바이트 배열로 변환
    utf8_bytes = encode_utf8(value)
    length = utf8_bytes.length

    // 길이를 먼저 직렬화 (ushort, 최대 65535 바이트)
    serialize_ushort(length)

    // UTF-8 바이트 복사
    for i from 0 to length - 1:
        buffer[offset + i] = utf8_bytes[i]
    offset += length

    return buffer

function deserialize_string(buffer):
    // 길이 읽기
    length = deserialize_ushort(buffer)

    // UTF-8 바이트 읽기
    utf8_bytes = new byte[length]
    for i from 0 to length - 1:
        utf8_bytes[i] = buffer[offset + i]
    offset += length

    // UTF-8 디코딩
    value = decode_utf8(utf8_bytes)
    return value

참고:
- 빈 문자열: length = 0
- null: length = 0xFFFF (65535)로 구분 (선택사항)
```

## 패킷 직렬화

### 패킷 헤더
```
슈도 코드:

struct PacketHeader:
    packet_type: ushort    // 2 bytes
    body_length: uint      // 4 bytes
    sequence: uint         // 4 bytes
    // 총 10 bytes

function serialize_header(header):
    serialize_ushort(header.packet_type)
    serialize_uint(header.body_length)
    serialize_uint(header.sequence)
    return buffer

function deserialize_header(buffer):
    header = new PacketHeader()
    header.packet_type = deserialize_ushort(buffer)
    header.body_length = deserialize_uint(buffer)
    header.sequence = deserialize_uint(buffer)
    return header
```

### 전체 패킷
```
슈도 코드:

function serialize_packet(packet):
    // Body 먼저 직렬화
    body_buffer = packet.serialize_body()

    // Header 생성
    header = new PacketHeader()
    header.packet_type = packet.type
    header.body_length = body_buffer.length
    header.sequence = get_next_sequence()

    // Header 직렬화
    header_buffer = serialize_header(header)

    // 결합
    final_buffer = concatenate(header_buffer, body_buffer)
    return final_buffer

function deserialize_packet(buffer):
    // Header 역직렬화
    header = deserialize_header(buffer)

    // Body 역직렬화
    body_buffer = buffer.slice(10, 10 + header.body_length)
    packet = create_packet_by_type(header.packet_type)
    packet.deserialize_body(body_buffer)

    return packet
```

## 복합 타입 직렬화

### 배열
```
슈도 코드:

function serialize_array<T>(array):
    // 배열 길이 (ushort)
    length = array.length
    serialize_ushort(length)

    // 각 요소 직렬화
    for i from 0 to length - 1:
        serialize_element(array[i])

    return buffer

function deserialize_array<T>(buffer):
    // 배열 길이 읽기
    length = deserialize_ushort(buffer)

    // 배열 생성 및 역직렬화
    array = new T[length]
    for i from 0 to length - 1:
        array[i] = deserialize_element<T>(buffer)

    return array
```

### 구조체/클래스
```
슈도 코드:

// 예시: Vector3
struct Vector3:
    x: float
    y: float
    z: float

function serialize_vector3(vec):
    serialize_float(vec.x)
    serialize_float(vec.y)
    serialize_float(vec.z)
    return buffer

function deserialize_vector3(buffer):
    vec = new Vector3()
    vec.x = deserialize_float(buffer)
    vec.y = deserialize_float(buffer)
    vec.z = deserialize_float(buffer)
    return vec
```

### 벡터 타입

#### Vector2 (8바이트)
```
슈도 코드:

struct Vector2:
    x: float    // 4 bytes
    y: float    // 4 bytes

function serialize_vector2(vec):
    serialize_float(vec.x)
    serialize_float(vec.y)
    return buffer

function deserialize_vector2(buffer):
    vec = new Vector2()
    vec.x = deserialize_float(buffer)
    vec.y = deserialize_float(buffer)
    return vec

// 바이트 예시:
// Vector2(1.0, 2.0)
// [00 00 80 3F] [00 00 00 40]
//  ^x (1.0)      ^y (2.0)
```

#### Vector4 (16바이트)
```
슈도 코드:

struct Vector4:
    x: float    // 4 bytes
    y: float    // 4 bytes
    z: float    // 4 bytes
    w: float    // 4 bytes

function serialize_vector4(vec):
    serialize_float(vec.x)
    serialize_float(vec.y)
    serialize_float(vec.z)
    serialize_float(vec.w)
    return buffer

function deserialize_vector4(buffer):
    vec = new Vector4()
    vec.x = deserialize_float(buffer)
    vec.y = deserialize_float(buffer)
    vec.z = deserialize_float(buffer)
    vec.w = deserialize_float(buffer)
    return vec
```

#### Quaternion (16바이트)
```
슈도 코드:

struct Quaternion:
    x: float    // 4 bytes
    y: float    // 4 bytes
    z: float    // 4 bytes
    w: float    // 4 bytes

function serialize_quaternion(quat):
    serialize_float(quat.x)
    serialize_float(quat.y)
    serialize_float(quat.z)
    serialize_float(quat.w)
    return buffer

function deserialize_quaternion(buffer):
    quat = new Quaternion()
    quat.x = deserialize_float(buffer)
    quat.y = deserialize_float(buffer)
    quat.z = deserialize_float(buffer)
    quat.w = deserialize_float(buffer)
    return quat
```

### 행렬 타입

#### Matrix2x2 (16바이트)
```
슈도 코드:

struct Matrix2x2:
    // Row-major order (행 우선)
    m00: float    m01: float
    m10: float    m11: float

function serialize_matrix2x2(mat):
    // Row-major order로 직렬화
    serialize_float(mat.m00)
    serialize_float(mat.m01)
    serialize_float(mat.m10)
    serialize_float(mat.m11)
    return buffer

function deserialize_matrix2x2(buffer):
    mat = new Matrix2x2()
    mat.m00 = deserialize_float(buffer)
    mat.m01 = deserialize_float(buffer)
    mat.m10 = deserialize_float(buffer)
    mat.m11 = deserialize_float(buffer)
    return mat

// 메모리 레이아웃:
// [m00][m01][m10][m11]
// 각 4바이트 float
```

#### Matrix3x3 (36바이트)
```
슈도 코드:

struct Matrix3x3:
    // Row-major order
    m00: float    m01: float    m02: float
    m10: float    m11: float    m12: float
    m20: float    m21: float    m22: float

function serialize_matrix3x3(mat):
    // Row-major order로 직렬화
    serialize_float(mat.m00)
    serialize_float(mat.m01)
    serialize_float(mat.m02)
    serialize_float(mat.m10)
    serialize_float(mat.m11)
    serialize_float(mat.m12)
    serialize_float(mat.m20)
    serialize_float(mat.m21)
    serialize_float(mat.m22)
    return buffer

function deserialize_matrix3x3(buffer):
    mat = new Matrix3x3()
    mat.m00 = deserialize_float(buffer)
    mat.m01 = deserialize_float(buffer)
    mat.m02 = deserialize_float(buffer)
    mat.m10 = deserialize_float(buffer)
    mat.m11 = deserialize_float(buffer)
    mat.m12 = deserialize_float(buffer)
    mat.m20 = deserialize_float(buffer)
    mat.m21 = deserialize_float(buffer)
    mat.m22 = deserialize_float(buffer)
    return mat

// 메모리 레이아웃:
// [m00][m01][m02]
// [m10][m11][m12]
// [m20][m21][m22]
// 순서대로 직렬화
```

#### Matrix4x4 (64바이트)
```
슈도 코드:

struct Matrix4x4:
    // Row-major order
    m00: float    m01: float    m02: float    m03: float
    m10: float    m11: float    m12: float    m13: float
    m20: float    m21: float    m22: float    m23: float
    m30: float    m31: float    m32: float    m33: float

function serialize_matrix4x4(mat):
    // Row-major order로 직렬화 (4x4 = 16개 float)
    for row from 0 to 3:
        for col from 0 to 3:
            serialize_float(mat[row][col])
    return buffer

function deserialize_matrix4x4(buffer):
    mat = new Matrix4x4()
    for row from 0 to 3:
        for col from 0 to 3:
            mat[row][col] = deserialize_float(buffer)
    return mat

// 메모리 레이아웃:
// [m00][m01][m02][m03]
// [m10][m11][m12][m13]
// [m20][m21][m22][m23]
// [m30][m31][m32][m33]
// 총 16개 float (64바이트)

참고:
- OpenGL: Column-major 사용
- DirectX/Unity: Row-major 사용
- 전송 시에는 항상 Row-major 사용
- 수신 후 필요시 전치(transpose) 적용
```

### 색상 타입

#### Color32 (4바이트, RGBA)
```
슈도 코드:

struct Color32:
    r: byte    // Red (0-255)
    g: byte    // Green (0-255)
    b: byte    // Blue (0-255)
    a: byte    // Alpha (0-255)

function serialize_color32(color):
    serialize_byte(color.r)
    serialize_byte(color.g)
    serialize_byte(color.b)
    serialize_byte(color.a)
    return buffer

function deserialize_color32(buffer):
    color = new Color32()
    color.r = deserialize_byte(buffer)
    color.g = deserialize_byte(buffer)
    color.b = deserialize_byte(buffer)
    color.a = deserialize_byte(buffer)
    return color

// 바이트 예시:
// Color32(255, 128, 64, 255)
// [FF] [80] [40] [FF]
//  ^R   ^G   ^B   ^A
```

#### Color (16바이트, RGBA float)
```
슈도 코드:

struct Color:
    r: float    // Red (0.0-1.0)
    g: float    // Green (0.0-1.0)
    b: float    // Blue (0.0-1.0)
    a: float    // Alpha (0.0-1.0)

function serialize_color(color):
    serialize_float(color.r)
    serialize_float(color.g)
    serialize_float(color.b)
    serialize_float(color.a)
    return buffer

function deserialize_color(buffer):
    color = new Color()
    color.r = deserialize_float(buffer)
    color.g = deserialize_float(buffer)
    color.b = deserialize_float(buffer)
    color.a = deserialize_float(buffer)
    return color
```

### 복합 배열 타입

#### 기본 타입 배열
```
슈도 코드:

// int 배열
function serialize_int_array(array):
    length = array.length
    serialize_ushort(length)

    for i from 0 to length - 1:
        serialize_int(array[i])

    return buffer

function deserialize_int_array(buffer):
    length = deserialize_ushort(buffer)
    array = new int[length]

    for i from 0 to length - 1:
        array[i] = deserialize_int(buffer)

    return array

// 바이트 예시:
// int[] {1, 2, 3}
// [03 00] [01 00 00 00] [02 00 00 00] [03 00 00 00]
//  ^len    ^1            ^2            ^3
```

#### 구조체 배열
```
슈도 코드:

// Vector3 배열
function serialize_vector3_array(array):
    length = array.length
    serialize_ushort(length)

    for i from 0 to length - 1:
        serialize_vector3(array[i])

    return buffer

function deserialize_vector3_array(buffer):
    length = deserialize_ushort(buffer)
    array = new Vector3[length]

    for i from 0 to length - 1:
        array[i] = deserialize_vector3(buffer)

    return array

// 바이트 예시:
// Vector3[] { (1,2,3), (4,5,6) }
// [02 00] [float][float][float] [float][float][float]
//  ^len    ^Vector3(1,2,3)       ^Vector3(4,5,6)
```

#### 클래스/구조체 배열 (가변 필드)
```
슈도 코드:

// ISerializable 인터페이스를 구현하는 클래스
interface ISerializable:
    function serialize(buffer, offset): int
    function deserialize(buffer, offset): int

struct Player implements ISerializable:
    id: string
    name: string
    position: Vector3
    health: int

    function serialize(buffer, offset):
        start_offset = offset
        offset = serialize_string(id, buffer, offset)
        offset = serialize_string(name, buffer, offset)
        offset = serialize_vector3(position, buffer, offset)
        offset = serialize_int(health, buffer, offset)
        return offset - start_offset  // 직렬화된 바이트 수 반환

    function deserialize(buffer, offset):
        start_offset = offset
        id = deserialize_string(buffer, ref offset)
        name = deserialize_string(buffer, ref offset)
        position = deserialize_vector3(buffer, ref offset)
        health = deserialize_int(buffer, ref offset)
        return offset - start_offset

// 클래스 배열 직렬화
function serialize_player_array(array):
    length = array.length
    serialize_ushort(length)

    for i from 0 to length - 1:
        array[i].serialize(buffer, offset)

    return buffer

function deserialize_player_array(buffer):
    length = deserialize_ushort(buffer)
    array = new Player[length]

    for i from 0 to length - 1:
        player = new Player()
        player.deserialize(buffer, offset)
        array[i] = player

    return array
```

#### 중첩 배열 (2차원 배열)
```
슈도 코드:

// int[][] 직렬화
function serialize_int_2d_array(array):
    // 외부 배열 길이
    outer_length = array.length
    serialize_ushort(outer_length)

    // 각 내부 배열 직렬화
    for i from 0 to outer_length - 1:
        inner_array = array[i]
        serialize_int_array(inner_array)

    return buffer

function deserialize_int_2d_array(buffer):
    // 외부 배열 길이
    outer_length = deserialize_ushort(buffer)
    array = new int[outer_length][]

    // 각 내부 배열 역직렬화
    for i from 0 to outer_length - 1:
        array[i] = deserialize_int_array(buffer)

    return array

// 바이트 예시:
// int[][] { {1,2}, {3,4,5} }
// [02 00] [02 00][01 00 00 00][02 00 00 00] [03 00][03 00 00 00][04 00 00 00][05 00 00 00]
//  ^outer  ^inner1 len        ^values       ^inner2 len          ^values
```

### Dictionary/Map 타입
```
슈도 코드:

// Dictionary<string, int>
function serialize_string_int_dictionary(dict):
    // 엔트리 개수
    count = dict.count
    serialize_ushort(count)

    // 각 키-값 쌍 직렬화
    for each entry in dict:
        serialize_string(entry.key)
        serialize_int(entry.value)

    return buffer

function deserialize_string_int_dictionary(buffer):
    // 엔트리 개수
    count = deserialize_ushort(buffer)
    dict = new Dictionary<string, int>()

    // 각 키-값 쌍 역직렬화
    for i from 0 to count - 1:
        key = deserialize_string(buffer)
        value = deserialize_int(buffer)
        dict.add(key, value)

    return dict

// 바이트 예시:
// Dictionary { "hp": 100, "mp": 50 }
// [02 00] [02 00][68 70][64 00 00 00] [02 00][6D 70][32 00 00 00]
//  ^count  ^"hp" len ^"hp"  ^100        ^"mp" len ^"mp"  ^50
```

### Nullable 타입
```
슈도 코드:

// Nullable<T>
function serialize_nullable<T>(value):
    // null 여부 플래그
    has_value = (value != null)
    serialize_bool(has_value)

    // 값이 있으면 직렬화
    if has_value:
        serialize<T>(value)

    return buffer

function deserialize_nullable<T>(buffer):
    // null 여부 플래그
    has_value = deserialize_bool(buffer)

    // 값이 있으면 역직렬화
    if has_value:
        value = deserialize<T>(buffer)
        return value
    else:
        return null

// 바이트 예시:
// Nullable<int>(100)
// [01] [64 00 00 00]
//  ^has_value ^100

// Nullable<int>(null)
// [00]
//  ^no value
```

## 패킷 예시

### LobbyConnect 패킷 (0x1001)
```
슈도 코드:

struct LobbyConnectRequest:
    token: string

function serialize(packet):
    offset = 0
    buffer = new byte[estimate_size()]

    serialize_string(packet.token)

    return buffer

function deserialize(buffer):
    offset = 0
    packet = new LobbyConnectRequest()

    packet.token = deserialize_string(buffer)

    return packet

// 바이트 스트림 예시:
// token = "abc123"
// [06 00] [61 62 63 31 32 33]
//  ^length  ^UTF-8 bytes
```

### StateSync 패킷 (0x3002)
```
슈도 코드:

struct PlayerState:
    player_id: string
    pos_x: float
    pos_y: float
    pos_z: float
    rotation: float

struct StateSyncPacket:
    players: array<PlayerState>
    timestamp: long

function serialize(packet):
    offset = 0
    buffer = new byte[estimate_size()]

    // 플레이어 배열 길이
    serialize_ushort(packet.players.length)

    // 각 플레이어 직렬화
    for each player in packet.players:
        serialize_string(player.player_id)
        serialize_float(player.pos_x)
        serialize_float(player.pos_y)
        serialize_float(player.pos_z)
        serialize_float(player.rotation)

    // 타임스탬프
    serialize_long(packet.timestamp)

    return buffer

function deserialize(buffer):
    offset = 0
    packet = new StateSyncPacket()

    // 플레이어 배열 길이
    count = deserialize_ushort(buffer)
    packet.players = new PlayerState[count]

    // 각 플레이어 역직렬화
    for i from 0 to count - 1:
        player = new PlayerState()
        player.player_id = deserialize_string(buffer)
        player.pos_x = deserialize_float(buffer)
        player.pos_y = deserialize_float(buffer)
        player.pos_z = deserialize_float(buffer)
        player.rotation = deserialize_float(buffer)
        packet.players[i] = player

    // 타임스탬프
    packet.timestamp = deserialize_long(buffer)

    return packet
```

## 검증 및 테스트

### 직렬화 동일성 검증
```
슈도 코드:

function test_serialization_identity<T>(value):
    // 직렬화
    buffer1 = serialize(value)

    // 역직렬화
    value2 = deserialize<T>(buffer1)

    // 재직렬화
    buffer2 = serialize(value2)

    // 바이트 비교
    assert(buffer1 == buffer2)

    // 값 비교
    assert(value == value2)
```

### 크로스 플랫폼 테스트
```
슈도 코드:

function test_cross_platform():
    // C# 서버에서 직렬화한 바이트
    csharp_bytes = [0x05, 0x00, 0x48, 0x65, 0x6C, 0x6C, 0x6F]

    // Unity 클라이언트에서 역직렬화
    value = deserialize_string(csharp_bytes)

    // 값 검증
    assert(value == "Hello")

    // Unity에서 재직렬화
    unity_bytes = serialize_string(value)

    // 바이트 동일성 검증
    assert(csharp_bytes == unity_bytes)
```

## 성능 최적화

### 버퍼 재사용
```
슈도 코드:

class PacketSerializer:
    buffer_pool: BufferPool

    function serialize(packet):
        // 풀에서 버퍼 가져오기
        buffer = buffer_pool.rent(packet.estimate_size())

        // 직렬화
        offset = 0
        packet.serialize_to(buffer, offset)

        return buffer

    function return_buffer(buffer):
        // 버퍼 풀에 반환
        buffer_pool.return(buffer)
```

### 예측 크기 계산
```
슈도 코드:

function estimate_packet_size(packet):
    size = 10  // Header size

    switch packet.type:
        case LobbyConnect:
            size += 2 + utf8_byte_count(packet.token)
        case StateSync:
            size += 2  // player count
            size += packet.players.length * (2 + 20 + 16)  // id + floats
            size += 8  // timestamp

    return size
```

## 언어별 구현 참고

### C#
```csharp
// BitConverter를 사용하되, Little Endian 보장
if (!BitConverter.IsLittleEndian)
    Array.Reverse(bytes);

// float/double 변환
int bits = BitConverter.SingleToInt32Bits(floatValue);
float value = BitConverter.Int32BitsToSingle(bits);
```

### Unity C#
```csharp
// C#과 동일하지만 Vector3 등 Unity 타입 추가 직렬화 필요
```

### Python
```python
import struct

# Little Endian 명시
struct.pack('<f', float_value)  # < = little endian
struct.unpack('<f', bytes)[0]
```

### JavaScript/TypeScript
```javascript
// DataView 사용 (Little Endian 명시)
const view = new DataView(buffer);
view.setFloat32(offset, value, true);  // true = little endian
const value = view.getFloat32(offset, true);
```

## 버전 관리

### 패킷 버전
```
슈도 코드:

const PROTOCOL_VERSION = 1

struct PacketHeader:
    version: byte          // 프로토콜 버전
    packet_type: ushort
    body_length: uint
    sequence: uint

// 버전 체크
function validate_packet_version(header):
    if header.version != PROTOCOL_VERSION:
        throw VersionMismatchError()
```
