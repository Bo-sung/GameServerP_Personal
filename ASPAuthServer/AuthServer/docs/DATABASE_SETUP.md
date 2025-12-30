# Database Setup Guide

이 문서는 AuthServer 프로젝트의 데이터베이스 설정 가이드입니다.

## 📋 목차

1. [필수 요구사항](#필수-요구사항)
2. [빠른 시작](#빠른-시작)
3. [데이터베이스 스키마](#데이터베이스-스키마)
4. [설정 방법](#설정-방법)
5. [테스트 계정](#테스트-계정)

---

## 📌 필수 요구사항

### 1. MySQL/MariaDB
- MySQL 8.0+ 또는 MariaDB 10.5+
- 설치: https://dev.mysql.com/downloads/mysql/

### 2. Redis
- Redis 6.0+
- Windows: https://github.com/microsoftarchive/redis/releases
- Linux/Mac: `apt-get install redis-server` / `brew install redis`

---

## 🚀 빠른 시작

### 방법 1: 자동 초기화 (권장)

애플리케이션을 실행하면 자동으로 데이터베이스와 테이블이 생성됩니다.

```bash
# appsettings.json의 ConnectionString만 설정 후
dotnet run
```

`DbInitializer` 클래스가 자동으로:
- 데이터베이스 생성 (없는 경우)
- Users 테이블 생성 (없는 경우)
- 기본 관리자 계정 생성 (admin/admin123)

### 방법 2: 수동 설치

```bash
# MySQL 접속
mysql -u root -p

# SQL 스크립트 실행
source setup_database.sql
```

---

## 📊 데이터베이스 스키마

### Users 테이블

| 컬럼명 | 타입 | 제약조건 | 설명 |
|--------|------|----------|------|
| Id | INT | PRIMARY KEY, AUTO_INCREMENT | 사용자 고유 ID |
| Username | VARCHAR(50) | NOT NULL, UNIQUE | 사용자명 (로그인 ID) |
| Email | VARCHAR(100) | NOT NULL, UNIQUE | 이메일 주소 |
| PasswordHash | VARCHAR(255) | NOT NULL | SHA256 해시된 비밀번호 |
| CreatedAt | DATETIME | NOT NULL | 계정 생성 시간 (UTC) |
| LastLoginAt | DATETIME | NULL | 마지막 로그인 시간 (UTC) |
| IsActive | TINYINT(1) | NOT NULL, DEFAULT 1 | 계정 활성화 여부 |
| LoginAttempts | INT | NOT NULL, DEFAULT 0 | 연속 로그인 실패 횟수 |
| LockedUntil | DATETIME | NULL | 계정 잠금 해제 시간 |

**인덱스:**
- `idx_username`: Username 컬럼
- `idx_email`: Email 컬럼
- `idx_isactive`: IsActive 컬럼

---

## ⚙️ 설정 방법

### 1. appsettings.json 설정

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=localhost;Port=3306;Database=authserver;User=root;Password=yourpassword;CharSet=utf8mb4;"
  },
  "RedisSettings": {
    "ConnectionString": "localhost:6379"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-change-this-in-production-min-32-chars",
    "Issuer": "AuthServer",
    "Audience": "GameClient",
    "LoginTokenExpirationMinutes": 5,
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30,
    "UsedLoginTokenRetentionHours": 24
  }
}
```

### 2. 보안 설정 체크리스트

- [ ] MySQL root 비밀번호 설정
- [ ] JWT SecretKey 변경 (최소 32자 이상)
- [ ] 기본 admin 계정 비밀번호 변경
- [ ] 프로덕션 환경에서 Redis 비밀번호 설정
- [ ] MySQL 사용자 권한 최소화 (root 대신 전용 계정 사용)

---

## 👤 테스트 계정

`setup_database.sql` 스크립트는 다음 계정을 생성합니다:

### 관리자 계정
- **Username**: `admin`
- **Password**: `admin123`
- **Email**: `admin@example.com`
- ⚠️ **반드시 비밀번호 변경 필요!**

### 테스트 계정
- **Username**: `testuser`
- **Password**: `test123`
- **Email**: `test@example.com`

---

## 🔒 Redis 키 구조

AuthServer는 다음과 같은 Redis 키를 사용합니다:

### Login Token (1회용)
```
login_token:active:{jti}     # 활성 로그인 토큰
login_token:used:{jti}       # 사용된 로그인 토큰 (재사용 방지)
```

### Refresh Token
```
refresh_token:{userId}:{deviceId}    # 사용자별 디바이스별 리프레시 토큰
```

### Access Token
- Redis에 저장되지 않음 (Stateless JWT)
- Signature로만 검증

---

## 🛠️ 문제 해결

### MySQL 연결 실패
```bash
# MySQL 서비스 상태 확인
# Windows
net start MySQL80

# Linux
sudo systemctl status mysql
```

### Redis 연결 실패
```bash
# Redis 서버 실행 확인
# Windows
redis-cli ping

# Linux
sudo systemctl status redis
```

### 테이블이 생성되지 않음
```sql
-- 수동으로 테이블 생성
USE authserver;
source database_schema.sql
```

---

## 📝 추가 정보

### 비밀번호 해싱 알고리즘
- SHA256 해시 함수 사용
- 1000회 반복 해싱 (iterations)
- Base64 인코딩

### 계정 잠금 정책
- 로그인 5회 실패 시 계정 잠금 (기본값)
- 잠금 시간: 15분 (기본값)
- `SecuritySettings`에서 설정 변경 가능

### 토큰 만료 시간 (기본값)
- Login Token: 5분
- Access Token: 60분
- Refresh Token: 30일
- Used Login Token 보관: 24시간

---

## 📚 관련 파일

- `database_schema.sql`: DDL 스키마 정의
- `setup_database.sql`: 즉시 실행 가능한 설치 스크립트
- `Data/DbInitializer.cs`: 자동 초기화 코드
- `Models/User.cs`: User 엔티티 모델
- `Data/Repositories/UserRepository.cs`: User CRUD 로직
