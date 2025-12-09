# API Reference

## 인증 서버 API

### Base URL
```
Production: https://api.example.com
Development: http://localhost:5000
```

### 공통 헤더
```
Content-Type: application/json
Accept: application/json
Authorization: Bearer {access_token}  // 인증 필요 시
```

---

## 인증 (Authentication)

### POST /api/auth/register
사용자 회원가입

#### 요청
```json
{
  "username": "string",      // 필수, 3-20자, 영문+숫자
  "email": "string",         // 필수, 유효한 이메일
  "password": "string",      // 필수, 8자 이상
  "nickname": "string"       // 선택, 2-20자
}
```

#### 응답 (201 Created)
```json
{
  "success": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Registration successful"
}
```

#### 에러 응답
```json
{
  "success": false,
  "errorCode": 2001,
  "message": "Username already exists",
  "details": {
    "field": "username"
  }
}
```

#### Rate Limit
- 3회/시간 (IP 기준)

---

### POST /api/auth/login
사용자 로그인

#### 요청
```json
{
  "username": "string",      // 필수
  "password": "string"       // 필수
}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4e5f6...",
  "expiresIn": 3600,         // 초 단위
  "tokenType": "Bearer",
  "user": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "username": "player1",
    "nickname": "Pro Gamer",
    "level": 15,
    "experience": 45000
  }
}
```

#### 에러 응답 (401 Unauthorized)
```json
{
  "success": false,
  "errorCode": 2001,
  "message": "Invalid username or password"
}
```

#### Rate Limit
- 5회/분 (IP 기준)

---

### POST /api/auth/refresh
Access Token 갱신

#### 요청
```json
{
  "refreshToken": "string"   // 필수
}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "b2c3d4e5f6a1...",  // 새로운 refresh token (선택사항)
  "expiresIn": 3600
}
```

#### 에러 응답 (401 Unauthorized)
```json
{
  "success": false,
  "errorCode": 2003,
  "message": "Invalid or expired refresh token"
}
```

#### Rate Limit
- 10회/분 (사용자 기준)

---

### POST /api/auth/logout
로그아웃

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 요청
```json
{
  "refreshToken": "string"   // 선택
}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

### POST /api/auth/verify
토큰 검증 (내부 API, 로비 서버 전용)

#### 요청
```json
{
  "token": "string"          // Access token
}
```

#### 응답 (200 OK)
```json
{
  "valid": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "username": "player1",
  "expiresAt": 1670003600
}
```

#### 에러 응답 (401 Unauthorized)
```json
{
  "valid": false,
  "errorCode": 2002,
  "message": "Invalid token"
}
```

---

## 사용자 관리 (User Management)

### GET /api/users/me
현재 사용자 정보 조회

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "user": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "username": "player1",
    "email": "player1@example.com",
    "nickname": "Pro Gamer",
    "level": 15,
    "experience": 45000,
    "stats": {
      "totalGames": 150,
      "wins": 90,
      "losses": 60,
      "winRate": 60.0,
      "mmr": 2500,
      "rank": "Gold III"
    },
    "createdAt": "2025-01-01T00:00:00Z",
    "lastLogin": "2025-12-09T10:30:45Z"
  }
}
```

---

### PUT /api/users/me
사용자 정보 수정

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 요청
```json
{
  "nickname": "string",      // 선택
  "email": "string"          // 선택
}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "message": "User updated successfully"
}
```

---

### PUT /api/users/me/password
비밀번호 변경

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 요청
```json
{
  "currentPassword": "string",  // 필수
  "newPassword": "string"       // 필수
}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "message": "Password changed successfully"
}
```

#### 에러 응답 (400 Bad Request)
```json
{
  "success": false,
  "errorCode": 2001,
  "message": "Current password is incorrect"
}
```

---

### GET /api/users/{userId}
다른 사용자 정보 조회 (공개 정보만)

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "user": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "username": "player1",
    "nickname": "Pro Gamer",
    "level": 15,
    "stats": {
      "totalGames": 150,
      "winRate": 60.0,
      "rank": "Gold III"
    }
  }
}
```

---

## 통계 (Statistics)

### GET /api/stats/leaderboard
리더보드 조회

#### 쿼리 파라미터
```
type: string (mmr, wins, level 등)
offset: int (기본값: 0)
limit: int (기본값: 100, 최대: 100)
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "leaderboard": [
    {
      "rank": 1,
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "username": "player1",
      "nickname": "Pro Gamer",
      "score": 3500,
      "level": 25
    },
    // ... 더 많은 항목
  ],
  "total": 10000,
  "offset": 0,
  "limit": 100
}
```

---

### GET /api/stats/me/history
내 게임 기록 조회

#### 헤더
```
Authorization: Bearer {access_token}
```

#### 쿼리 파라미터
```
offset: int (기본값: 0)
limit: int (기본값: 20, 최대: 50)
```

#### 응답 (200 OK)
```json
{
  "success": true,
  "games": [
    {
      "gameId": "game_123",
      "gameMode": "mode1",
      "result": "win",
      "score": 1500,
      "rank": 1,
      "playedAt": "2025-12-09T10:00:00Z",
      "duration": 1200
    },
    // ... 더 많은 게임
  ],
  "total": 150,
  "offset": 0,
  "limit": 20
}
```

---

## 에러 코드

### 인증 에러 (2xxx)
| 코드 | 설명 |
|-----|------|
| 2001 | 인증 실패 (잘못된 사용자명 또는 비밀번호) |
| 2002 | 잘못된 토큰 |
| 2003 | 만료된 토큰 |
| 2004 | 권한 없음 |
| 2005 | 사용자 이미 존재 |
| 2006 | 이메일 이미 존재 |

### 검증 에러 (3xxx)
| 코드 | 설명 |
|-----|------|
| 3001 | 잘못된 요청 형식 |
| 3002 | 필수 필드 누락 |
| 3003 | 유효하지 않은 필드 값 |
| 3004 | 필드 길이 초과 |

### 서버 에러 (5xxx)
| 코드 | 설명 |
|-----|------|
| 5000 | 서버 내부 오류 |
| 5001 | 데이터베이스 오류 |
| 5002 | 외부 서비스 오류 |

### Rate Limit 에러 (4xxx)
| 코드 | 설명 |
|-----|------|
| 4001 | Rate limit 초과 |
| 4002 | 일일 요청 한도 초과 |

---

## 에러 응답 형식

모든 에러 응답은 다음 형식을 따릅니다:

```json
{
  "success": false,
  "errorCode": 3001,
  "message": "Invalid request format",
  "details": {
    "field": "username",
    "reason": "Username must be between 3 and 20 characters"
  }
}
```

---

## Rate Limiting

### 응답 헤더
```
X-RateLimit-Limit: 5           // 시간 윈도우 내 최대 요청 수
X-RateLimit-Remaining: 3       // 남은 요청 수
X-RateLimit-Reset: 1670000060  // 리셋 시간 (Unix timestamp)
```

### Rate Limit 초과 시
```
HTTP/1.1 429 Too Many Requests
Retry-After: 60                // 재시도까지 대기 시간 (초)

{
  "success": false,
  "errorCode": 4001,
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 60
}
```

---

## 페이지네이션

리스트를 반환하는 모든 API는 다음 페이지네이션 형식을 따릅니다:

### 요청 파라미터
```
offset: 시작 위치 (기본값: 0)
limit: 항목 수 (기본값: API마다 다름, 최대값 명시)
```

### 응답 형식
```json
{
  "success": true,
  "data": [ /* 항목들 */ ],
  "pagination": {
    "total": 10000,      // 전체 항목 수
    "offset": 0,         // 현재 시작 위치
    "limit": 100,        // 요청한 항목 수
    "hasMore": true      // 더 많은 항목이 있는지 여부
  }
}
```

---

## 필터링 및 정렬

일부 API는 필터링 및 정렬을 지원합니다:

### 쿼리 파라미터
```
sort: 정렬 필드 (예: level, wins, created_at)
order: asc 또는 desc (기본값: desc)
filter: 필터 조건 (API마다 다름)
```

### 예시
```
GET /api/stats/leaderboard?type=mmr&sort=score&order=desc&offset=0&limit=100
```

---

## 웹훅 (향후 지원 예정)

게임 이벤트를 외부 서비스로 전송하는 웹훅 기능은 향후 추가될 예정입니다.

---

## SDK 및 클라이언트 라이브러리

### C# Client (향후 제공)
```csharp
var client = new GameServerClient("https://api.example.com");
var response = await client.Auth.LoginAsync("username", "password");
```

### Unity Client (향후 제공)
```csharp
var api = new GameAPI();
await api.LoginAsync("username", "password");
```

---

## 변경 이력

### v1.0.0 (2025-01-01)
- 초기 API 릴리스
- 인증, 사용자 관리, 통계 API
