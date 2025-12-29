# AuthServer API 문서

## 개요
ASP.NET Core 기반 게임 인증 서버 RESTful API 문서입니다.

---

## 🎮 게임 클라이언트 API (`/api/auth`)

### 1. 회원가입
**Endpoint:** `POST /api/auth/register`

**Request Body:**
```json
{
  "username": "string",
  "email": "string (optional)",
  "password": "string (min 6 chars)"
}
```

**Response (200 OK):**
```json
{
  "userId": 1,
  "username": "player123",
  "message": "회원가입 성공"
}
```

**Error Responses:**
- `400 Bad Request`: 중복된 사용자명/이메일, 비밀번호 강도 부족

---

### 2. 로그인
**Endpoint:** `POST /api/auth/login`

**Request Body:**
```json
{
  "username": "string",
  "password": "string",
  "deviceId": "string"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." // Login Token (1분 유효)
}
```

**Error Responses:**
- `401 Unauthorized`: 로그인 실패, 비밀번호 불일치
- `423 Locked`: 계정 잠김 (로그인 시도 초과)

**비고:**
- Login Token은 1회용이며, 1분 내에 Exchange API로 교환 필요
- 로그인 실패 5회 시 계정 5분간 잠김

---

### 3. 토큰 교환 (Login Token → Access + Refresh Token)
**Endpoint:** `POST /api/auth/exchange`

**Request Body:**
```json
{
  "loginToken": "string",
  "deviceId": "string"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // 15분 유효
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." // 7일 유효
}
```

**Error Responses:**
- `401 Unauthorized`: 유효하지 않은 Login Token, 만료된 토큰, 재사용 시도

**비고:**
- Login Token은 1회용이며 재사용 불가
- Exchange 후 Login Token은 자동으로 폐기됨

---

### 4. 토큰 갱신 (Access Token 갱신)
**Endpoint:** `POST /api/auth/refresh`

**Request Body:**
```json
{
  "refreshToken": "string",
  "deviceId": "string"
}
```

**Response (200 OK):**
```json
{
  "message": "Token Refreshed",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." // 새로운 Access Token (15분 유효)
}
```

**Error Responses:**
- `401 Unauthorized`: 유효하지 않은 Refresh Token, 만료된 토큰

---

### 5. 로그아웃
**Endpoint:** `POST /api/auth/logout`

**Headers:**
```
Authorization: Bearer <AccessToken>
```

**Request Body:**
```json
{
  "deviceId": "string"
}
```

**Response (200 OK):**
```json
{
  "message": "로그아웃 성공"
}
```

**Error Responses:**
- `400 Bad Request`: Authorization 헤더 누락
- `401 Unauthorized`: 유효하지 않은 Access Token
- `500 Internal Server Error`: 로그아웃 처리 실패

**비고:**
- 로그아웃 시 해당 디바이스의 Refresh Token이 Redis에서 삭제됨
- Access Token은 stateless이므로 만료까지 유효하나, Refresh Token 삭제로 갱신 불가

---

## 🛠️ 관리자 API (`/api/admin`)

### 1. 관리자 로그인
**Endpoint:** `POST /api/admin/login`

**Request Body:**
```json
{
  "username": "string",
  "password": "string",
  "deviceId": "string (optional)"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // Admin Login Token
  "expiresIn": 1
}
```

**Error Responses:**
- `401 Unauthorized`: 관리자 인증 실패
- `403 Forbidden`: 관리자 권한 없음

---

### 2. 관리자 토큰 교환
**Endpoint:** `POST /api/admin/exchange`

**Request Body:**
```json
{
  "loginToken": "string",
  "deviceId": "string"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // 15분 유효
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." // 1일 유효 (관리자는 짧게)
}
```

---

### 3. 전체 사용자 조회
**Endpoint:** `GET /api/admin/users`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Query Parameters:**
- `page`: 페이지 번호 (기본값: 1)
- `pageSize`: 페이지 크기 (기본값: 10, 최대: 100)
- `search`: 검색어 (username 또는 email)
- `isActive`: 활성 상태 필터 (true/false)

**Response (200 OK):**
```json
{
  "totalCount": 150,
  "page": 1,
  "pageSize": 10,
  "totalPages": 15,
  "users": [
    {
      "id": 1,
      "username": "player123",
      "email": "player@example.com",
      "isActive": true,
      "createdAt": "2025-12-01T10:00:00Z",
      "lastLoginAt": "2025-12-28T15:30:00Z",
      "loginAttempts": 0,
      "lockedUntil": null
    }
  ]
}
```

---

### 4. 특정 사용자 조회
**Endpoint:** `GET /api/admin/users/{userId}`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Response (200 OK):**
```json
{
  "id": 1,
  "username": "player123",
  "email": "player@example.com",
  "isActive": true,
  "createdAt": "2025-12-01T10:00:00Z",
  "lastLoginAt": "2025-12-28T15:30:00Z",
  "loginAttempts": 0,
  "lockedUntil": null
}
```

**Error Responses:**
- `404 Not Found`: 사용자를 찾을 수 없음

---

### 5. 사용자 계정 잠금/해제
**Endpoint:** `PATCH /api/admin/users/{userId}/lock`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Request Body:**
```json
{
  "lock": true, // true: 잠금, false: 해제
  "durationMinutes": 30 // 잠금 시간 (분), lock=true일 때만 필요
}
```

**Response (200 OK):**
```json
{
  "message": "사용자 계정이 잠겼습니다.",
  "userId": 1,
  "lockedUntil": "2025-12-28T16:30:00Z"
}
```

---

### 6. 사용자 비밀번호 초기화
**Endpoint:** `POST /api/admin/users/{userId}/reset-password`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Request Body:**
```json
{
  "newPassword": "string (min 6 chars)"
}
```

**Response (200 OK):**
```json
{
  "message": "비밀번호가 초기화되었습니다.",
  "userId": 1
}
```

---

### 7. 사용자 세션 강제 종료
**Endpoint:** `DELETE /api/admin/users/{userId}/sessions`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Query Parameters:**
- `deviceId`: 특정 디바이스만 종료 (선택사항, 없으면 모든 세션 종료)

**Response (200 OK):**
```json
{
  "message": "사용자의 모든 세션이 종료되었습니다.",
  "userId": 1,
  "sessionsTerminated": 3
}
```

---

### 8. 사용자 삭제
**Endpoint:** `DELETE /api/admin/users/{userId}`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Response (200 OK):**
```json
{
  "message": "사용자가 삭제되었습니다.",
  "userId": 1
}
```

**Error Responses:**
- `404 Not Found`: 사용자를 찾을 수 없음

---

### 9. 서버 통계 조회
**Endpoint:** `GET /api/admin/statistics`

**Headers:**
```
Authorization: Bearer <AdminAccessToken>
```

**Response (200 OK):**
```json
{
  "totalUsers": 1500,
  "activeUsers": 1200,
  "lockedUsers": 15,
  "onlineUsers": 245,
  "todayRegistrations": 20,
  "todayLogins": 450
}
```

---

## 🔒 인증 및 권한

### 토큰 종류
1. **Login Token**: 로그인 직후 발급되는 1회용 단기 토큰 (1분)
2. **Access Token**: API 접근을 위한 단기 토큰 (15분)
3. **Refresh Token**: Access Token 갱신을 위한 장기 토큰 (게임: 7일, 관리자: 1일)

### 토큰 저장 위치
- **Login Token**: Redis (active/used 상태 추적)
- **Access Token**: Stateless (검증만 수행, 저장 안 함)
- **Refresh Token**: Redis (userId + deviceId 기반 키)

### 관리자 권한 검증
- Admin API는 `AdminJwtSettings`로 발급된 토큰만 허용
- Audience: `AdminPanel`
- 일반 게임 토큰으로는 접근 불가

---

## 📊 에러 코드

| HTTP 상태 | 에러 코드 | 설명 |
|-----------|-----------|------|
| 400 | REGISTER_FAILED | 회원가입 실패 (중복, 유효성 검증 실패) |
| 401 | LOGIN_FAILED | 로그인 실패 (비밀번호 불일치) |
| 401 | INVALID_LOGIN_TOKEN | 유효하지 않은 Login Token |
| 401 | TOKEN_EXCHANGE_FAILED | 토큰 교환 실패 |
| 401 | INVALID_REFRESH_TOKEN | 유효하지 않은 Refresh Token |
| 401 | TOKEN_REFRESH_FAILED | 토큰 갱신 실패 |
| 401 | INVALID_TOKEN | 유효하지 않은 토큰 |
| 401 | LOGOUT_FAILED | 로그아웃 처리 실패 |
| 403 | FORBIDDEN | 권한 없음 (관리자 전용 API) |
| 404 | NOT_FOUND | 리소스를 찾을 수 없음 |
| 423 | ACCOUNT_LOCKED | 계정 잠김 |

---

## 🔧 개발 환경 설정

### Base URL
- **개발 환경**: `http://localhost:5000`
- **프로덕션**: TBD

### 설정 파일 (`appsettings.json`)
```json
{
  "GameJwtSettings": {
    "Audience": "GameClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "AdminJwtSettings": {
    "Audience": "AdminPanel",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 1
  }
}
```

---

## 📝 참고사항

### 로그인 플로우 (게임 클라이언트)
1. `POST /api/auth/login` → Login Token 받기
2. `POST /api/auth/exchange` → Access Token + Refresh Token 받기
3. API 호출 시 `Authorization: Bearer <AccessToken>` 헤더 사용
4. Access Token 만료 시 `POST /api/auth/refresh` → 새 Access Token 받기
5. Refresh Token 만료 시 다시 로그인 필요

### 로그인 플로우 (관리자 툴)
1. `POST /api/admin/login` → Admin Login Token 받기
2. `POST /api/admin/exchange` → Admin Access Token + Refresh Token 받기
3. Admin API 호출 시 `Authorization: Bearer <AdminAccessToken>` 헤더 사용

### 보안 주의사항
- Login Token은 1회용이므로 재사용 금지
- Refresh Token은 안전하게 저장 (LocalStorage 지양, HttpOnly Cookie 권장)
- Admin 토큰은 더 짧은 수명으로 관리 (Refresh Token 1일)
