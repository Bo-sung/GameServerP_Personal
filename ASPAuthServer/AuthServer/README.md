# Auth Server

ASP.NET Core 기반의 RESTful 인증 서버

## 기술 스택

- Framework: ASP.NET Core 8.0
- Database: MySQL 8.0+
- Cache: Redis
- 언어: C# 12

## 주요 기능

- RESTful API 설계 원칙 적용
- 회원가입 / 로그인 / 로그아웃
- JWT 토큰 기반 인증 (예정)
- Redis 세션 관리
- 계정 잠금 정책 (로그인 실패 3회 시 15분 잠금)
- 데이터베이스 자동 초기화
- Options 패턴을 통한 설정 관리
- Dependency Injection (DI)

## 프로젝트 구조

```
AuthServer/
├── Controllers/           # API 컨트롤러
│   ├── AuthController.cs    # 인증 API
│   └── AdminController.cs   # 관리자 API
├── Data/                  # 데이터 액세스 레이어
│   ├── DbConnectionFactory.cs
│   ├── DbInitializer.cs     # DB 초기화
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
├── Settings/              # 설정 클래스
│   ├── DatabaseSettings.cs
│   ├── JwtSettings.cs
│   ├── SecuritySettings.cs
│   ├── SessionSettings.cs
│   └── RateLimitSettings.cs
├── docs/                  # 문서
│   └── API.md            # API 문서
└── Program.cs            # 진입점
```

## 시작하기

### 사전 요구사항

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8.0+
- Redis 6.0+

### 1. 설정 파일 구성

`appsettings.json` 파일을 생성하거나 수정합니다:

```json
{
  "DatabaseSettings": {
    "MySQLConnection": "Server=localhost;Port=3306;Database=authserver;User=root;Password=yourpassword;",
    "RedisConnection": "localhost:6379",
    "MySQLConnectionPoolSize": 10,
    "RedisConnectionPoolSize": 10,
    "EnableConnectionRetry": true,
    "MaxRetryAttempts": 3
  },
  "GameJwtSettings": {
    "SecretKey": "your-super-secret-key-min-32-chars",
    "Issuer": "AuthServer",
    "Audience": "GameClient",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  },
  "AdminJwtSettings": {
    "SecretKey": "your-admin-secret-key-min-32-chars",
    "Issuer": "AuthServer",
    "Audience": "AdminPanel",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "SecuritySettings": {
    "PasswordHashIterations": 1000,
    "MaxLoginAttempts": 3,
    "LockoutDurationMinutes": 15
  },
  "SessionSettings": {
    "SessionTimeoutMinutes": 60,
    "EnableSlidingExpiration": true
  },
  "RateLimitSettings": {
    "RequestsPerMinute": 60,
    "BurstSize": 10
  }
}
```

### 2. 데이터베이스 준비

MySQL과 Redis가 실행 중인지 확인합니다.

```bash
# MySQL 실행 확인
mysql -u root -p -e "SELECT VERSION();"

# Redis 실행 확인
redis-cli ping
```

데이터베이스는 애플리케이션 시작 시 자동으로 생성됩니다.

### 3. 애플리케이션 실행

```bash
# 프로젝트 복원
dotnet restore

# 빌드
dotnet build

# 실행
dotnet run
```

애플리케이션이 시작되면 다음과 같은 출력을 볼 수 있습니다:

```
[DB 초기화] 시작...
  데이터베이스 'authserver' 생성 중...
  데이터베이스 'authserver' 생성 완료
  테이블 'Users' 생성 중...
  테이블 'Users' 생성 완료
  기본 관리자 계정 생성 중...
  기본 관리자 계정 생성 완료 (ID: admin, PW: admin123)
  보안을 위해 비밀번호를 즉시 변경하세요!
[DB 초기화] 완료

info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
```

### 4. API 테스트

#### 회원가입
```bash
curl -X POST https://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "password123"
  }' \
  -k -v
```

응답:
```http
HTTP/1.1 201 Created
Location: https://localhost:5001/api/auth/users/2
Content-Type: application/json

{
  "userId": 2,
  "username": "testuser",
  "message": "회원가입 성공"
}
```

#### 사용자 정보 조회 (RESTful)
```bash
curl -X GET https://localhost:5001/api/auth/users/2 -k
```

#### 로그인
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "password": "password123"
  }' \
  -k
```

더 많은 API 예시는 [API 문서](docs/API.md) 참고

## 기본 관리자 계정

애플리케이션 첫 실행 시 기본 관리자 계정이 자동으로 생성됩니다:

- Username: admin
- Password: admin123
- Email: admin@example.com

주의: 보안을 위해 초기 비밀번호를 즉시 변경할 것

## RESTful API 설계

RESTful API 설계 원칙을 따름:

### 리소스 중심 설계
```
GET    /api/auth/users/{id}    - 사용자 조회
POST   /api/auth/register      - 사용자 생성 (회원가입)
POST   /api/auth/login         - 인증 (로그인)
POST   /api/auth/logout        - 세션 종료
GET    /api/auth/verify        - 토큰 검증
```

### HTTP 상태 코드
- `200 OK` - 요청 성공
- `201 Created` - 리소스 생성 성공 (Location 헤더 포함)
- `400 Bad Request` - 잘못된 요청
- `401 Unauthorized` - 인증 실패
- `404 Not Found` - 리소스 없음

### Location 헤더
리소스 생성 시 Location 헤더에 생성된 리소스의 URL을 포함합니다:

```http
POST /api/auth/register
→ 201 Created
→ Location: https://localhost:5001/api/auth/users/123
```

자세한 내용은 [API 문서](docs/API.md) 참고

## 아키텍처 패턴

### Dependency Injection
```csharp
// Program.cs
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

### Repository 패턴
```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<int> CreateAsync(User user);
    // ...
}
```

### Options 패턴
```csharp
builder.Services.AddOptions<DatabaseSettings>()
    .Bind(builder.Configuration.GetSection("DatabaseSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## 보안 기능

### 1. 비밀번호 보안
- SHA256 해싱 (1000 iterations)
- 프로덕션 환경에서는 BCrypt 권장

### 2. 계정 보호
- 로그인 실패 3회 시 15분 잠금
- 잠금 시간 동안 로그인 불가

### 3. 세션 관리
- Redis 기반 세션 저장
- TTL 1시간 (설정 가능)
- 로그아웃 시 즉시 무효화

## 개발

### 빌드
```bash
dotnet build
```

### 테스트
```bash
dotnet test
```

### 프로덕션 빌드
```bash
dotnet publish -c Release -o ./publish
```

## 문서

- [API 문서](docs/API.md) - 전체 API 엔드포인트와 사용 예시
- [아키텍처 설명](docs/ARCHITECTURE.md) - 프로젝트 구조와 설계 패턴 (예정)

## 라이센스

이 프로젝트는 개인 프로젝트입니다.

## 기여

프로젝트에 대한 피드백이나 제안은 이슈를 통해 남겨주세요.

---

주의사항:
- 프로덕션 환경에서는 HTTPS 사용 필수
- 기본 관리자 계정의 비밀번호를 즉시 변경
- JWT Secret Key는 충분히 복잡하게 설정 (최소 32자)
- 비밀번호 해싱은 BCrypt 또는 PBKDF2 사용 권장
