# 게임 인증 서버 설계 문서

## 프로젝트 개요

게임에서 사용할 인증 서버를 ASP.NET Core 8.0 + Redis + MySQL 기반으로 구현

기술 스택:
- ASP.NET Core 8.0
- MySQL (사용자 계정 정보)
- Redis (세션 및 토큰 관리)
- JWT (Json Web Token)

---

## 핵심 기능 요구사항

### 1. 데이터베이스 구성

#### 1.1 로그인 정보 DB (MySQL)
- 사용자 계정 정보 영구 저장
- ID, 비밀번호(해시), 계정 상태 등

#### 1.2 세션 정보 DB (Redis)
- 활성 세션 관리
- JWT Refresh Token 저장
- 임시 데이터 캐싱

### 2. 회원가입 기능

3가지 가입 옵션 지원:

| 옵션 | ID | 비밀번호 | 사용 사례 |
|------|----|----|---------|
| 옵션 1 | 사용자 지정 | 사용자 지정 | 일반 회원가입 |
| 옵션 2 | 사용자 지정 | 자동 생성 | 간편 가입 (이메일/SMS로 PW 전송) |
| 옵션 3 | 자동 생성 | 자동 생성 | 게스트 계정 |

기능:
- ID 중복 체크
- 비밀번호 해싱 (bcrypt/Argon2)
- 계정 생성 시 기본 상태 설정

### 3. 로그인 기능

입력: ID, 비밀번호
출력: JWT Access Token + Refresh Token

프로세스:
1. ID/PW 검증
2. 로그인 실패 횟수 확인
3. JWT Access Token 발급 (만료: 15분~1시간)
4. Refresh Token 발급 및 Redis 저장 (만료: 7~30일)
5. 세션 정보 Redis 저장

### 4. 자동 로그인 기능

방식 1: Access Token 재사용
- 유효한 Access Token 전달 시 바로 인증 통과
- Token 만료 시 실패

방식 2: Refresh Token으로 갱신
- Access Token 만료 시 Refresh Token으로 새 Access Token 발급
- Refresh Token도 만료되면 재로그인 필요

---

## 보안 요구사항

### 비밀번호 보안
- bcrypt/Argon2 해싱 + Salt 적용
- 평문 저장 금지
- 비밀번호 노출 방지

### JWT 토큰 전략
```
Access Token:  짧은 만료 시간 (15분~1시간)
Refresh Token: 긴 만료 시간 (7~30일), Redis에 저장 및 검증
```

토큰 무효화:
- 로그아웃 시 Redis에서 Refresh Token 삭제
- 필요시 Access Token 블랙리스트 운영

### 로그인 보안
- 로그인 실패 횟수 제한 (예: 5회 실패 시 계정 잠금 5분)
- Rate Limiting (IP당 요청 제한)
- 의심스러운 로그인 시도 로깅

### 중복 로그인 처리
정책 선택:
- 옵션 A: 중복 로그인 허용 (여러 디바이스 동시 접속)
- 옵션 B: 신규 로그인 시 기존 세션 강제 종료
- 옵션 C: 동일 디바이스만 중복 허용

---

## 데이터베이스 스키마

### MySQL - Users 테이블
```sql
CREATE TABLE users (
    user_id BIGINT PRIMARY KEY AUTO_INCREMENT,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(100),
    user_status ENUM('active', 'suspended', 'deleted') DEFAULT 'active',
    login_fail_count INT DEFAULT 0,
    locked_until DATETIME NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    last_login_at DATETIME NULL
);
```

### Redis - 세션 및 토큰
```
Key Pattern:
- session:{user_id}        → 세션 정보 (JSON)
- refresh_token:{user_id}  → Refresh Token
- token_blacklist:{token}  → 블랙리스트 (로그아웃한 토큰)
- login_attempts:{ip}      → IP별 로그인 시도 횟수
```

---

## API 설계

### RESTful 설계 원칙 적용

이 프로젝트는 RESTful API 설계 원칙을 따릅니다:
- 리소스 중심 URL 설계
- 적절한 HTTP 메서드 사용 (GET, POST, PUT, DELETE)
- HTTP 상태 코드 활용 (200 OK, 201 Created, 400 Bad Request, 401 Unauthorized, 404 Not Found)
- Location 헤더를 통한 리소스 위치 제공

### 사용자 조회 (RESTful) - 구현 완료
```
GET /api/auth/users/{id}
Path Parameter:
- id: 사용자 ID (int)

Response (200 OK):
{
  "userId": "12345",
  "username": "user123",
  "email": "user@example.com",
  "createdAt": "2025-01-15T10:30:00Z"
}

Response (404 Not Found):
{
  "code": "USER_NOT_FOUND",
  "message": "사용자를 찾을 수 없습니다."
}
```

### 회원가입 - 구현 완료
```
POST /api/auth/register
Request Body:
{
  "username": "string",
  "password": "string",
  "email": "string (optional)"
}

Response (201 Created):
HTTP/1.1 201 Created
Location: https://localhost:5001/api/auth/users/12345

{
  "userId": 12345,
  "username": "user123",
  "message": "회원가입 성공"
}

Response (400 Bad Request):
{
  "code": "REGISTER_FAILED",
  "message": "이미 존재하는 사용자명 또는 이메일입니다."
}
```

RESTful 특징:
- 201 Created 상태 코드 반환
- Location 헤더에 생성된 리소스 URL 포함 (`/api/auth/users/{id}`)
- 클라이언트는 Location URL로 GET 요청하여 생성된 사용자 정보 조회 가능

### 로그인 - 구현 완료
```
POST /api/auth/login
Request Body:
{
  "username": "string",
  "password": "string"
}

Response (200 OK):
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}

Response (401 Unauthorized):
{
  "code": "LOGIN_FAILED",
  "message": "비밀번호가 일치하지 않습니다. (남은 시도: 2회)"
}
```

### 토큰 검증 - 구현 완료
```
GET /api/auth/verify
Request Header:
Authorization: Bearer {accessToken}

Response (200 OK):
{
  "userId": "12345",
  "username": "user123",
  "email": "user@example.com",
  "createdAt": "2025-01-15T10:30:00Z"
}

Response (401 Unauthorized):
{
  "code": "INVALID_TOKEN",
  "message": "유효하지 않은 토큰입니다."
}
```

### 토큰 갱신 - 구현 예정
```
POST /api/auth/refresh
Request Body:
{
  "refreshToken": "string"
}

Response (200 OK):
{
  "accessToken": "eyJhbGc...",
  "expiresIn": 3600
}

Response (401 Unauthorized):
{
  "code": "INVALID_REFRESH_TOKEN",
  "message": "리프레시 토큰이 유효하지 않습니다."
}
```

### 로그아웃 - 구현 완료
```
POST /api/auth/logout
Request Header:
Authorization: Bearer {accessToken}

Response (200 OK):
{
  "message": "로그아웃 성공"
}

Response (401 Unauthorized):
{
  "code": "INVALID_TOKEN",
  "message": "유효하지 않은 토큰입니다."
}
```

### HTTP 상태 코드 사용 가이드

| 상태 코드 | 의미 | 사용 예시 |
|----------|------|----------|
| 200 OK | 요청 성공 | 로그인, 토큰 검증, 로그아웃 |
| 201 Created | 리소스 생성 성공 | 회원가입 |
| 400 Bad Request | 잘못된 요청 | 유효성 검증 실패 |
| 401 Unauthorized | 인증 실패 | 잘못된 비밀번호, 유효하지 않은 토큰 |
| 404 Not Found | 리소스 없음 | 존재하지 않는 사용자 |
| 500 Internal Server Error | 서버 오류 | 예상치 못한 에러 |

---

## 에러 응답 표준

```json
{
  "success": false,
  "errorCode": "AUTH_001",
  "message": "아이디 또는 비밀번호가 올바르지 않습니다.",
  "details": null
}
```

에러 코드 예시:
- `AUTH_001`: 로그인 실패 (잘못된 자격증명)
- `AUTH_002`: 계정 잠김
- `AUTH_003`: 토큰 만료
- `AUTH_004`: 유효하지 않은 토큰
- `AUTH_005`: ID 중복
- `AUTH_006`: 세션 없음

---

## 구현 우선순위

### Phase 1: 핵심 기능 (필수) - 완료
- [x] MySQL 사용자 테이블 설계 및 구현
- [x] 비밀번호 해싱 (SHA256, 1000 iterations)
- [x] 회원가입 API (수동 방식)
- [x] 로그인 API
- [x] Redis 세션 저장
- [x] 로그아웃 API
- [x] 데이터베이스 자동 초기화
- [x] RESTful API 설계 적용
- [x] 사용자 조회 API (GET /api/auth/users/{id})
- [x] Dependency Injection 구조
- [x] Options 패턴 적용
- [ ] JWT Access Token + Refresh Token 구현 (임시 토큰 사용 중)

### Phase 2: 보안 강화 (권장) - 부분 완료
- [x] 로그인 실패 횟수 제한 (3회)
- [x] 계정 잠금 기능 (15분)
- [ ] Rate Limiting
- [ ] 토큰 블랙리스트
- [ ] 중복 로그인 처리 정책
- [ ] 로그인 이력 저장

### Phase 3: 확장 기능 (선택)
- [ ] 회원가입 옵션 2, 3 (자동 생성)
- [ ] 게스트 계정 → 정식 계정 전환
- [ ] 비밀번호 재설정 기능
- [ ] 소셜 로그인 연동
- [ ] 다중 디바이스 세션 관리

### 현재 구현 상태

완료된 기능:
- MySQL + Redis 기반 인증 서버 구조
- RESTful API 설계 원칙 적용
- 회원가입 (201 Created + Location 헤더)
- 로그인 (Redis 세션 관리)
- 로그아웃 (세션 무효화)
- 토큰 검증
- 사용자 조회 API
- 계정 잠금 정책 (로그인 실패 3회 → 15분 잠금)
- DB 자동 초기화 (테이블 생성 + 기본 관리자 계정)
- Repository 패턴
- Options 패턴을 통한 설정 관리

진행 중:
- JWT 토큰 (현재 임시 토큰 사용)
- Refresh Token 갱신 로직

계획 중:
- Rate Limiting
- 토큰 블랙리스트
- 중복 로그인 처리

---

## 기술 스택 상세

### NuGet 패키지 (예상)
```xml
<!-- JWT -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" />

<!-- MySQL -->
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" />

<!-- Redis -->
<PackageReference Include="StackExchange.Redis" />

<!-- 비밀번호 해싱 -->
<PackageReference Include="BCrypt.Net-Next" />

<!-- Rate Limiting -->
<PackageReference Include="AspNetCoreRateLimit" />
```

---

## 참고사항

### JWT Payload 예시
```json
{
  "sub": "12345",
  "username": "user123",
  "iat": 1234567890,
  "exp": 1234571490,
  "type": "access"
}
```

---

## RESTful API 설계 상세

### Location 헤더 사용 예시

회원가입 성공 시:
```http
POST /api/auth/register HTTP/1.1
Content-Type: application/json

{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "password123"
}

↓

HTTP/1.1 201 Created
Location: https://localhost:5001/api/auth/users/123
Content-Type: application/json

{
  "userId": 123,
  "username": "john_doe",
  "message": "회원가입 성공"
}
```

클라이언트는 Location URL로 생성된 리소스 조회:
```http
GET /api/auth/users/123 HTTP/1.1

↓

HTTP/1.1 200 OK
Content-Type: application/json

{
  "userId": "123",
  "username": "john_doe",
  "email": "john@example.com",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

### 리소스 중심 URL 구조

```
/api/auth/users/{id}     → 사용자 리소스 (GET)
/api/auth/register       → 사용자 생성 액션 (POST)
/api/auth/login          → 인증 액션 (POST)
/api/auth/logout         → 세션 종료 액션 (POST)
/api/auth/verify         → 토큰 검증 액션 (GET)
/api/auth/refresh        → 토큰 갱신 액션 (POST)
```

---

## 프로젝트 구조

```
AuthServer/
├── Controllers/           # API 컨트롤러
│   ├── AuthController.cs    # 인증 API (RESTful)
│   └── AdminController.cs   # 관리자 API
├── Data/                  # 데이터 액세스 레이어
│   ├── DbConnectionFactory.cs
│   ├── DbInitializer.cs     # DB 초기화 (Main에서만 실행)
│   ├── RedisConnectionFactory.cs
│   └── Repositories/
│       ├── MySQLWrapper.cs  # MySQL 쿼리 래퍼
│       ├── UserRepository.cs
│       └── IUserRepository.cs
├── Models/                # 데이터 모델
│   ├── User.cs
│   └── AuthModels.cs
├── Services/              # 비즈니스 로직
│   ├── AuthService.cs
│   └── IAuthService.cs
├── Settings/              # 설정 클래스 (Options 패턴)
│   ├── DatabaseSettings.cs
│   ├── JwtSettings.cs
│   ├── SecuritySettings.cs
│   ├── SessionSettings.cs
│   └── RateLimitSettings.cs
├── docs/                  # 문서
│   └── API.md            # API 상세 문서
├── AUTH_SERVER_DESIGN.md  # 이 문서
├── README.md             # 프로젝트 시작 가이드
└── Program.cs            # 진입점 + DI 설정
```

---

## 문서 링크

- [API 문서](docs/API.md) - 전체 API 엔드포인트 상세 설명 및 사용 예시
- [README.md](README.md) - 프로젝트 시작 가이드 및 설정 방법
- [AUTH_SERVER_DESIGN.md](AUTH_SERVER_DESIGN.md) - 이 설계 문서
