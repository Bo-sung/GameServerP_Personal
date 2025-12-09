# 보안 가이드

## JWT 토큰 구조

### Access Token (JWT)
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "user_id",           // Subject (사용자 ID)
    "username": "player1",      // 사용자명
    "iat": 1670000000,          // Issued At (발급 시간)
    "exp": 1670003600,          // Expiration (만료 시간, 1시간)
    "jti": "unique_token_id"    // JWT ID (고유 식별자)
  },
  "signature": "..."
}
```

### Refresh Token
- **저장소**: MySQL `refresh_tokens` 테이블
- **형식**: SHA256 해시값
- **만료 시간**: 30일
- **용도**: Access Token 갱신

### 토큰 발급 프로세스
```
1. 로그인 성공
2. Access Token 생성 (1시간 유효)
3. Refresh Token 생성 (30일 유효)
4. Refresh Token을 DB에 저장 (해시값)
5. 둘 다 클라이언트에 반환
```

### 토큰 갱신 프로세스
```
1. Access Token 만료
2. 클라이언트가 Refresh Token으로 갱신 요청
3. DB에서 Refresh Token 검증
4. 새로운 Access Token 발급
5. (선택) 새로운 Refresh Token 발급 (Refresh Token Rotation)
```

## 비밀번호 보안

### 해싱 알고리즘
- **알고리즘**: BCrypt
- **Work Factor**: 12 (2^12 iterations)
- **Salt**: BCrypt 자동 생성 (랜덤 salt)

### 비밀번호 정책
- **최소 길이**: 8자
- **권장 길이**: 12자 이상
- **복잡도**: 영문 대소문자, 숫자, 특수문자 중 3종 이상
- **만료**: 90일마다 변경 권장 (선택사항)
- **재사용 방지**: 최근 5개 비밀번호 재사용 불가

### C# 구현 예시
```csharp
using BCrypt.Net;

// 비밀번호 해싱
string hashedPassword = BCrypt.HashPassword(plainPassword, workFactor: 12);

// 비밀번호 검증
bool isValid = BCrypt.Verify(plainPassword, hashedPassword);
```

## Rate Limiting

### API Rate Limit
```
인증 서버:
- 로그인: 5회/분 (IP 기준)
- 회원가입: 3회/시간 (IP 기준)
- 토큰 갱신: 10회/분 (사용자 기준)

로비 서버:
- 방 생성: 10회/분 (사용자 기준)
- 방 참가: 20회/분 (사용자 기준)

게임 서버:
- 게임 이벤트: 100회/초 (연결 기준)
```

### Redis를 이용한 Rate Limiting
```
키 패턴: ratelimit:{resource}:{identifier}
타입: String
값: 요청 횟수
TTL: 시간 윈도우 (60초, 3600초 등)

예시:
SET ratelimit:login:192.168.1.1 1 EX 60 NX
INCR ratelimit:login:192.168.1.1
GET ratelimit:login:192.168.1.1
```

### C# 구현 예시
```csharp
public async Task<bool> CheckRateLimit(string resource, string identifier, int limit, int windowSeconds)
{
    var key = $"ratelimit:{resource}:{identifier}";

    var current = await redis.StringGetAsync(key);

    if (current.IsNullOrEmpty)
    {
        await redis.StringSetAsync(key, 1, TimeSpan.FromSeconds(windowSeconds));
        return true;
    }

    var count = (int)current;
    if (count >= limit)
    {
        return false; // Rate limit exceeded
    }

    await redis.StringIncrementAsync(key);
    return true;
}
```

## DDoS 방어

### L7 DDoS 방어
1. **Rate Limiting**: API 엔드포인트별 요청 제한
2. **IP 화이트리스트/블랙리스트**: 의심스러운 IP 차단
3. **CAPTCHA**: 의심스러운 활동 감지 시 CAPTCHA 요구
4. **Connection Throttling**: 동일 IP에서 과도한 연결 제한

### TCP SYN Flood 방어
```
OS 레벨 설정 (Linux):
sysctl -w net.ipv4.tcp_syncookies=1
sysctl -w net.ipv4.tcp_max_syn_backlog=2048
sysctl -w net.ipv4.tcp_synack_retries=2
```

### UDP Flood 방어 (ENet)
- 연결 전 Challenge-Response 인증
- 패킷 크기 제한 (최대 1KB)
- IP당 연결 수 제한

## 입력 검증

### 클라이언트 입력 검증 원칙
```
모든 클라이언트 입력은 서버에서 검증
- 길이 검증
- 타입 검증
- 범위 검증
- 형식 검증 (정규식)
- 비즈니스 로직 검증
```

### SQL Injection 방어
```csharp
// Parameterized Query 사용
var query = "SELECT * FROM users WHERE username = @username";
var command = new MySqlCommand(query, connection);
command.Parameters.AddWithValue("@username", username);
```

### XSS 방어
```csharp
// 채팅 메시지 HTML 이스케이프
using System.Net;
string safeMessage = WebUtility.HtmlEncode(userMessage);
```

### Command Injection 방어
```csharp
// 외부 명령 실행 금지
// 필요시 화이트리스트 기반 검증
var allowedCommands = new[] { "start", "stop", "status" };
if (!allowedCommands.Contains(userCommand))
{
    throw new SecurityException("Invalid command");
}
```

## 통신 보안

### HTTPS (인증 서버)
- **프로토콜**: TLS 1.2 이상
- **인증서**: Let's Encrypt 또는 유료 인증서
- **암호화 스위트**: AES-GCM 우선

### TCP/UDP (게임 서버)
- **암호화**: 선택사항 (성능 vs 보안 트레이드오프)
- **체크섬**: 패킷 무결성 검증
- **시퀀스 번호**: 패킷 순서 및 재전송 공격 방지

### 패킷 검증
```csharp
// 체크섬 계산 (예시: CRC32)
public uint CalculateChecksum(byte[] data)
{
    var crc = new Crc32();
    crc.Append(data);
    return BitConverter.ToUInt32(crc.GetCurrentHash());
}

// 패킷 검증
public bool ValidatePacket(byte[] packet, uint expectedChecksum)
{
    var checksum = CalculateChecksum(packet);
    return checksum == expectedChecksum;
}
```

## 세션 보안

### 세션 하이재킹 방어
1. **세션 토큰 갱신**: 권한 상승 시 새 토큰 발급
2. **IP 바인딩**: 세션과 IP 주소 연결 (선택사항)
3. **User-Agent 검증**: 세션 생성 시 User-Agent 저장 및 검증
4. **타임아웃**: 비활성 세션 자동 만료 (30분)

### Redis 세션 보안
```
세션 키: session:{session_id}
추가 필드:
- ip_address: 생성 시 IP
- user_agent: 생성 시 User-Agent
- last_activity: 마지막 활동 시간
- is_locked: 의심스러운 활동 시 잠금
```

## 권한 관리

### 역할 기반 접근 제어 (RBAC)
```
역할:
- Admin: 모든 권한
- Moderator: 유저 관리, 채팅 관리
- User: 기본 게임 플레이
- Guest: 읽기 전용

권한 확인:
if (!user.HasPermission("create_room"))
{
    return ErrorResponse(ErrorCode.Forbidden);
}
```

### JWT 클레임에 역할 포함
```json
{
  "sub": "user_id",
  "username": "player1",
  "role": "user",
  "permissions": ["play_game", "create_room", "join_room"]
}
```

## 로그 및 감사

### 보안 이벤트 로깅
```
로깅 대상:
- 로그인 성공/실패
- 비밀번호 변경
- 권한 변경
- 의심스러운 활동 (다중 실패, 비정상적 패턴)
- 관리자 작업
```

### 로그 형식
```json
{
  "timestamp": "2025-12-09T10:30:45Z",
  "event_type": "login_failed",
  "user_id": "user123",
  "ip_address": "192.168.1.100",
  "details": {
    "reason": "invalid_password",
    "attempt_count": 3
  }
}
```

### 로그 보관
- **기간**: 90일
- **저장소**: 파일 또는 중앙 로그 시스템 (ELK Stack)
- **암호화**: 민감 정보 마스킹

## 취약점 대응

### 정기 보안 점검
1. **의존성 업데이트**: NuGet 패키지 정기 업데이트
2. **보안 스캔**: OWASP Dependency-Check
3. **침투 테스트**: 주기적 모의 해킹
4. **코드 리뷰**: 보안 중심 코드 리뷰

### 보안 패치 프로세스
```
1. 취약점 발견
2. 영향 범위 분석
3. 패치 개발 및 테스트
4. 긴급 배포
5. 사용자 알림
```

## 개인정보 보호

### GDPR 준수 (해당 시)
- **데이터 최소화**: 필요한 정보만 수집
- **동의**: 명시적 동의 획득
- **삭제 권리**: 사용자 요청 시 데이터 삭제
- **암호화**: 민감 정보 암호화 저장

### 데이터 보관
```
MySQL:
- 비밀번호: BCrypt 해시 (복호화 불가)
- 이메일: 암호화 저장 (AES-256)
- IP 주소: 로그에만 저장 (30일 후 삭제)

Redis:
- 세션: TTL 자동 만료
- 임시 데이터: 24시간 이내 삭제
```

## 보안 체크리스트

### 배포 전 체크리스트
- [ ] 모든 비밀번호가 BCrypt로 해싱되는가?
- [ ] JWT 토큰에 민감 정보가 없는가?
- [ ] HTTPS가 활성화되어 있는가?
- [ ] Rate Limiting이 적용되어 있는가?
- [ ] SQL Injection 방어가 되어 있는가?
- [ ] XSS 방어가 되어 있는가?
- [ ] 모든 사용자 입력이 검증되는가?
- [ ] 에러 메시지에 민감 정보가 노출되지 않는가?
- [ ] 로그에 비밀번호/토큰이 기록되지 않는가?
- [ ] 기본 관리자 계정이 변경되었는가?

### 운영 중 모니터링
- [ ] 비정상적인 로그인 시도 모니터링
- [ ] API Rate Limit 초과 모니터링
- [ ] 서버 리소스 사용량 모니터링
- [ ] 보안 패치 알림 확인
- [ ] 로그 파일 정기 검토

## 사고 대응 계획

### 보안 사고 대응 절차
```
1. 사고 감지 및 확인
2. 영향 범위 파악
3. 긴급 조치 (서버 차단, 계정 정지 등)
4. 로그 수집 및 분석
5. 패치 및 복구
6. 사용자 알림
7. 사후 분석 및 개선
```

### 연락처
```
보안 담당자: security@example.com
긴급 연락처: +82-10-XXXX-XXXX
```
