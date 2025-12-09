AuthServer (간단 스캐폴드)

설명:
- 이 프로젝트는 최소한의 인증 서비스 골격입니다.
- 제공 엔드포인트:
  - POST /api/auth/login  -> { username, password } 반환: accessToken, refreshToken
  - POST /api/auth/verify -> { token } 반환: valid, userId
  - POST /api/auth/refresh -> { refreshToken } 반환: accessToken

설정:
- `appsettings.json`의 `ConnectionStrings:AuthDatabase`를 실제 MySQL 접속 문자열로 수정하세요.
- `Jwt:Key`는 배포 전 반드시 안전한 비밀키로 변경하세요.

실행:
```cmd
cd /d h:\Git\GameServerP_Personal\authLobbyGame\Project\Server\AuthServer
dotnet restore
dotnet run
```

참고:
- 현재 구현은 기본 검증 및 refresh token 저장(해시) 로직을 포함합니다.
- 운영 환경에서는 HTTPS 활성화, JWT 키 관리(RS256 권장), Refresh token rotation, rate limiting 등을 추가하세요.
