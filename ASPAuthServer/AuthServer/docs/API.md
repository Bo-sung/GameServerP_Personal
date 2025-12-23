# Auth Server API 문서

RESTful API 설계 원칙을 따르는 인증 서버 API 문서

## 기본 정보

- Base URL: `https://localhost:5001/api/auth`
- Content-Type: `application/json`
- 인증 방식: Bearer Token (Authorization 헤더)

---

## 엔드포인트 목록

### 1. 사용자 조회 (RESTful)

GET /api/auth/users/{id}

생성된 사용자 정보를 조회합니다.

경로 파라미터:
| 파라미터 | 타입 | 필수 | 설명 |
|---------|------|------|------|
| id | int | O | 사용자 ID |

응답 성공 (200 OK):
```json
{
  "userId": "123",
  "username": "john_doe",
  "email": "john@example.com",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

응답 실패 (404 Not Found):
```json
{
  "code": "USER_NOT_FOUND",
  "message": "사용자를 찾을 수 없습니다."
}
```

예시 요청:
```bash
curl -X GET https://localhost:5001/api/auth/users/123
```

---

### 2. 회원가입

POST /api/auth/register

새로운 사용자를 생성합니다. RESTful 원칙에 따라 201 Created와 Location 헤더를 반환합니다.

요청 본문:
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "password123"
}
```

필드 설명:
| 필드 | 타입 | 필수 | 제약사항 |
|------|------|------|---------|
| username | string | O | 고유값, 1-50자 |
| email | string | O | 고유값, 이메일 형식 |
| password | string | O | 최소 6자 이상 |

응답 성공 (201 Created):
```http
HTTP/1.1 201 Created
Location: https://localhost:5001/api/auth/users/123
Content-Type: application/json

{
  "userId": 123,
  "username": "john_doe",
  "message": "회원가입 성공"
}
```

응답 실패 (400 Bad Request):
```json
{
  "code": "REGISTER_FAILED",
  "message": "이미 존재하는 사용자명 또는 이메일입니다."
}
```

예시 요청:
```bash
curl -X POST https://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "email": "john@example.com",
    "password": "password123"
  }'
```

RESTful 흐름:
```
1. POST /api/auth/register → 201 Created + Location 헤더
2. Location 헤더의 URL로 조회 가능
3. GET /api/auth/users/123 → 생성된 사용자 정보
```

---

### 3. 로그인

POST /api/auth/login

사용자 인증 후 액세스 토큰을 발급합니다.

요청 본문:
```json
{
  "username": "john_doe",
  "password": "password123"
}
```

응답 성공 (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

응답 실패 (401 Unauthorized):
```json
{
  "code": "LOGIN_FAILED",
  "message": "비밀번호가 일치하지 않습니다. (남은 시도: 2회)"
}
```

계정 잠금 응답:
```json
{
  "code": "LOGIN_FAILED",
  "message": "계정이 잠겼습니다. 15분 후 다시 시도하세요."
}
```

예시 요청:
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "password": "password123"
  }'
```

---

### 4. 로그아웃

POST /api/auth/logout

현재 세션을 종료하고 토큰을 무효화합니다.

헤더:
```
Authorization: Bearer {access_token}
```

응답 성공 (200 OK):
```json
{
  "message": "로그아웃 성공"
}
```

응답 실패 (400 Bad Request):
```json
{
  "code": "INVALID_TOKEN",
  "message": "토큰이 제공되지 않았습니다."
}
```

응답 실패 (401 Unauthorized):
```json
{
  "code": "INVALID_TOKEN",
  "message": "유효하지 않은 토큰입니다."
}
```

예시 요청:
```bash
curl -X POST https://localhost:5001/api/auth/logout \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

### 5. 토큰 검증

GET /api/auth/verify

현재 토큰이 유효한지 확인하고 사용자 정보를 반환합니다.

헤더:
```
Authorization: Bearer {access_token}
```

응답 성공 (200 OK):
```json
{
  "userId": "123",
  "username": "john_doe",
  "email": "john@example.com",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

응답 실패 (401 Unauthorized):
```json
{
  "code": "INVALID_TOKEN",
  "message": "유효하지 않은 토큰입니다."
}
```

예시 요청:
```bash
curl -X GET https://localhost:5001/api/auth/verify \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

### 6. 토큰 갱신

POST /api/auth/refresh

리프레시 토큰을 사용하여 새로운 액세스 토큰을 발급받습니다.

요청 본문:
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

응답 성공 (200 OK):
```json
{
  "message": "Refresh endpoint ready"
}
```

주의: 현재 구현 중

예시 요청:
```bash
curl -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'
```

---

## RESTful API 설계 원칙

### HTTP 상태 코드

| 상태 코드 | 의미 | 사용 예시 |
|----------|------|----------|
| 200 OK | 요청 성공 | 로그인, 토큰 검증 |
| 201 Created | 리소스 생성 성공 | 회원가입 |
| 400 Bad Request | 잘못된 요청 | 유효성 검증 실패 |
| 401 Unauthorized | 인증 실패 | 잘못된 비밀번호, 유효하지 않은 토큰 |
| 404 Not Found | 리소스 없음 | 존재하지 않는 사용자 |
| 500 Internal Server Error | 서버 오류 | 예상치 못한 에러 |

### Location 헤더

리소스 생성 시 (201 Created) Location 헤더에 생성된 리소스의 URL을 포함합니다.

예시:
```http
POST /api/auth/register

HTTP/1.1 201 Created
Location: https://localhost:5001/api/auth/users/123
```

클라이언트는 Location 헤더의 URL로 GET 요청을 보내 생성된 리소스를 조회할 수 있습니다.

---

## 에러 응답 형식

모든 에러는 일관된 형식으로 반환됩니다.

```json
{
  "code": "ERROR_CODE",
  "message": "사용자 친화적인 에러 메시지"
}
```

에러 코드 목록:
| 코드 | 설명 |
|------|------|
| REGISTER_FAILED | 회원가입 실패 |
| LOGIN_FAILED | 로그인 실패 |
| INVALID_TOKEN | 유효하지 않은 토큰 |
| USER_NOT_FOUND | 사용자를 찾을 수 없음 |

---

## 보안 고려사항

### 1. 계정 잠금 정책
- 로그인 실패 3회 시 계정 15분 잠금
- 잠금 시간 동안 로그인 시도 불가

### 2. 비밀번호 보안
- SHA256 해싱 사용 (1000 iterations)
- 최소 6자 이상 요구
- 프로덕션 환경에서는 BCrypt 또는 PBKDF2 권장

### 3. 토큰 관리
- Redis에 세션 저장 (1시간 TTL)
- 로그아웃 시 토큰 즉시 무효화

### 4. HTTPS 필수
- 프로덕션 환경에서는 반드시 HTTPS 사용
- 토큰과 비밀번호가 평문으로 전송되므로 필수

---

## 데이터베이스 초기화

애플리케이션 시작 시 자동으로 데이터베이스와 테이블을 생성합니다.

기본 관리자 계정:
- Username: admin
- Password: admin123
- Email: admin@example.com

주의: 보안을 위해 초기 비밀번호를 즉시 변경할 것

---

## 예시 시나리오

### 회원가입 → 로그인 → 정보 조회 전체 흐름

```bash
# 1. 회원가입
curl -X POST https://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "email": "john@example.com",
    "password": "password123"
  }'

# 응답:
# HTTP/1.1 201 Created
# Location: https://localhost:5001/api/auth/users/123
# { "userId": 123, "username": "john_doe", "message": "회원가입 성공" }

# 2. Location 헤더의 URL로 사용자 정보 조회 (RESTful)
curl -X GET https://localhost:5001/api/auth/users/123

# 응답:
# { "userId": "123", "username": "john_doe", "email": "john@example.com", ... }

# 3. 로그인
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "password": "password123"
  }'

# 응답:
# { "accessToken": "eyJ...", "refreshToken": "eyJ...", "expiresIn": 3600 }

# 4. 토큰으로 인증된 요청
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X GET https://localhost:5001/api/auth/verify \
  -H "Authorization: Bearer $TOKEN"

# 5. 로그아웃
curl -X POST https://localhost:5001/api/auth/logout \
  -H "Authorization: Bearer $TOKEN"
```

---

## 추가 참고 자료

- [RESTful API 설계 가이드](https://restfulapi.net/)
- [HTTP 상태 코드](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status)
- [JWT 토큰 소개](https://jwt.io/introduction)

---
