# GM Tool - 프로젝트 개요

ASP.NET Core AuthServer를 위한 WPF 기반 관리자 도구

---

## 📚 설계 문서

1. **[DESIGN_DOCUMENT.md](DESIGN_DOCUMENT.md)**
   - WPF 프로젝트 전체 설계
   - MVVM 아키텍처
   - 인증 시스템 구현
   - 주요 화면 구성

2. **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)**
   - 중저사양 PC 최적화
   - 플랫 디자인 가이드
   - DataGrid 가상화
   - 메모리 관리

3. **[LOG_SYSTEM_DESIGN.md](LOG_SYSTEM_DESIGN.md)** 🆕
   - 하단 고정 로그 뷰어
   - 실시간 로그 모니터링
   - API 호출, 에러, 사용자 액션 추적
   - 로그 필터링 및 검색

4. **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)**
   - AuthServer API 명세서

---

## 🎯 주요 기능

### ✅ 인증 시스템
- 관리자 로그인 (Admin Login Token → Access + Refresh Token)
- 자동 토큰 갱신 (401 시 Refresh Token 사용)
- 토큰 만료 시 자동 로그아웃

### ✅ 사용자 관리
- 사용자 목록 조회 (페이지네이션, 검색, 필터)
- 사용자 상세 정보
- 계정 잠금/해제
- 비밀번호 초기화
- 세션 강제 종료
- 사용자 삭제

### ✅ 대시보드
- 서버 통계 (총 사용자, 활성 사용자, 온라인 사용자 등)
- 실시간 통계 새로고침

### 🆕 로그 시스템
- **하단 고정 로그 뷰어** (모든 페이지에서 유지)
- API 호출 로그 (요청/응답)
- 에러 추적
- 사용자 액션 로그
- 로그 레벨 필터 (Debug, Info, Success, Warning, Error)
- 로그 검색 및 클리어

---

## 🏗️ 프로젝트 구조 (간략)

```
GMTool/
├── Models/           # 데이터 모델
├── ViewModels/       # MVVM ViewModels
│   └── LogViewModel.cs  # 🆕 로그 뷰어 ViewModel
├── Views/
│   ├── LoginWindow.xaml     # 로그인 (로그 포함)
│   ├── MainWindow.xaml      # 메인 (로그 포함)
│   ├── Pages/
│   └── Controls/
│       └── LogViewer.xaml   # 🆕 로그 뷰어 UserControl
├── Services/
│   ├── Auth/
│   ├── User/
│   ├── Statistics/
│   └── Logging/      # 🆕 로그 서비스
│       ├── ILogService.cs
│       ├── LogService.cs
│       └── LogEntry.cs
└── Infrastructure/
    ├── Http/         # TokenRefreshHandler
    └── Token/        # TokenManager
```

---

## 🎨 UI 레이아웃

### LoginWindow (로그인 창)
```
┌─────────────────────────────────────┐
│         로그인 화면 (중앙)            │
│     [Username]                      │
│     [Password]                      │
│     [로그인 버튼]                     │
├─────────────────────────────────────┤
│  📝 로그 영역 (200px, 고정)          │
│  [12:34:56] ℹ️ 로그인 시도: admin    │
│  [12:34:57] ✅ 로그인 성공            │
└─────────────────────────────────────┘
```

### MainWindow (메인 창)
```
┌─────────────────────────────────────┐
│ [사이드바] │   페이지 콘텐츠          │
│ Dashboard │   (DashboardPage,       │
│ 사용자관리  │    UserListPage 등)     │
├─────────────────────────────────────┤
│  📝 로그 영역 (리사이즈 가능)         │
│  [🔍] 검색   [🗑️] 클리어   [레벨▼]   │
│  [12:35:10] 🔍 GET /api/admin/users │
│  [12:35:11] ✅ 사용자 목록 로드 (50건)│
└─────────────────────────────────────┘
```

---

## 🚀 시작하기

### 1. 프로젝트 생성
```bash
cd h:\Git\GameServerP_Personal\ASPAuthServer\GMTool

# WPF 프로젝트 생성
dotnet new wpf -n GMTool -f net8.0-windows

cd GMTool
```

### 2. NuGet 패키지 설치
```bash
dotnet add package ModernWpfUI
dotnet add package Newtonsoft.Json
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Http
```

### 3. 설계 문서 참고하여 구현
- [DESIGN_DOCUMENT.md](DESIGN_DOCUMENT.md) 참고
- [LOG_SYSTEM_DESIGN.md](LOG_SYSTEM_DESIGN.md) 참고

---

## 🔧 기술 스택

- **.NET 8.0 WPF**
- **ModernWpfUI** (Fluent Design)
- **MVVM 패턴** (CommunityToolkit.Mvvm)
- **의존성 주입** (Microsoft.Extensions.DependencyInjection)
- **HttpClient** + **DelegatingHandler** (토큰 자동 갱신)

---

## 📝 로그 시스템 사용 예시

### 서비스에서 로그 기록
```csharp
public class AuthService : IAuthService
{
    private readonly ILogService _logService;

    public async Task<string> LoginAsync(string username, string password)
    {
        _logService.Info($"로그인 시도: {username}");

        // ... API 호출 ...

        _logService.Success($"로그인 성공: {username}");
        return loginToken;
    }
}
```

### 로그 출력 예시
```
[12:34:56] ℹ️ 로그인 시도: admin
[12:34:57] 🔍 POST /api/admin/login
[12:34:57] ✅ 로그인 성공: admin
[12:34:58] 🔍 POST /api/admin/exchange
[12:34:58] ✅ Access Token 획득 완료
[12:35:10] 🔍 GET /api/admin/users?page=1&pageSize=20
[12:35:11] ✅ 사용자 목록 로드 완료: 150건
[12:50:30] ⚠️ Access Token 만료, 갱신 시도 중...
[12:50:31] ✅ Access Token 갱신 성공
```

---

## 📌 개발 순서 권장

1. ✅ 기본 WPF 프로젝트 생성
2. ✅ Models 작성 (DTOs)
3. ✅ 로그 시스템 구현 (LogService, LogViewer)
4. ✅ TokenManager 구현
5. ✅ AuthService 구현
6. ✅ LoginWindow (로그 포함)
7. ✅ MainWindow (로그 포함)
8. ✅ DashboardPage
9. ✅ UserListPage
10. ✅ UserDetailPage

---

## 🎨 성능 최적화 원칙

- ✅ Drop Shadow, Blur 제거
- ✅ DataGrid 가상화
- ✅ 페이지네이션 (20개/페이지)
- ✅ 로그 최대 500개 제한
- ✅ Binding Mode 최적화
- ✅ API 호출 캐싱

---

## 📞 API 서버 설정

AuthServer가 `http://localhost:5000`에서 실행 중이어야 합니다.

```bash
# AuthServer 실행 (별도 터미널)
cd h:\Git\GameServerP_Personal\ASPAuthServer
dotnet run
```

---

## 📖 참고 문서

- [ModernWpfUI GitHub](https://github.com/Kinnara/ModernWpf)
- [CommunityToolkit.Mvvm Docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [Microsoft DI Docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
