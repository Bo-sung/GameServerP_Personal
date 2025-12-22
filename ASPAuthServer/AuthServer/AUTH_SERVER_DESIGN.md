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

### 회원가입
```
POST /api/auth/register
Request Body:
{
  "username": "string (optional)",
  "password": "string (optional)",
  "email": "string (optional)",
  "registrationType": "manual" | "auto_password" | "guest"
}

Response:
{
  "success": true,
  "userId": 12345,
  "username": "user123",
  "generatedPassword": "auto_pw" (옵션 2, 3인 경우)
}
```

### 로그인
```
POST /api/auth/login
Request Body:
{
  "username": "string",
  "password": "string"
}

Response:
{
  "success": true,
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

### 자동 로그인 (토큰 검증)
```
POST /api/auth/verify
Request Header:
Authorization: Bearer {accessToken}

Response:
{
  "success": true,
  "userId": 12345,
  "username": "user123"
}
```

### 토큰 갱신
```
POST /api/auth/refresh
Request Body:
{
  "refreshToken": "string"
}

Response:
{
  "success": true,
  "accessToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

### 로그아웃
```
POST /api/auth/logout
Request Header:
Authorization: Bearer {accessToken}

Request Body:
{
  "refreshToken": "string"
}

Response:
{
  "success": true
}
```

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

### Phase 1: 핵심 기능 (필수)
- [ ] MySQL 사용자 테이블 설계 및 구현
- [ ] 비밀번호 해싱 (bcrypt)
- [ ] 회원가입 API (옵션 1: 수동)
- [ ] 로그인 API + JWT 발급
- [ ] JWT Access Token + Refresh Token 구현
- [ ] Redis 세션 저장
- [ ] 로그아웃 API

### Phase 2: 보안 강화 (권장)
- [ ] 로그인 실패 횟수 제한
- [ ] 계정 잠금 기능
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