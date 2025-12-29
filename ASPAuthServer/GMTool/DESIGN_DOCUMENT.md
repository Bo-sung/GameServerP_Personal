# GM Tool 설계 문서

## 프로젝트 개요
ASP.NET Core AuthServer를 위한 WPF 기반 관리자 도구(GM Tool)

### 기술 스택
- **프레임워크**: .NET 8.0 WPF
- **아키텍처**: MVVM 패턴
- **UI 라이브러리**: ModernWpfUI (Windows 10/11 Fluent Design)
- **HTTP 통신**: HttpClient + Newtonsoft.Json
- **MVVM 헬퍼**: CommunityToolkit.Mvvm
- **의존성 주입**: Microsoft.Extensions.DependencyInjection

---

## 프로젝트 구조

```
GMTool/
├── GMTool.sln
├── GMTool/
│   ├── App.xaml                      # 애플리케이션 진입점
│   ├── App.xaml.cs                   # DI 컨테이너 설정
│   ├── MainWindow.xaml               # 메인 윈도우 (네비게이션 컨테이너)
│   ├── MainWindow.xaml.cs
│   │
│   ├── Models/                       # 데이터 모델
│   │   ├── Auth/
│   │   │   ├── LoginRequest.cs       # 로그인 요청 DTO
│   │   │   ├── LoginResponse.cs      # 로그인 응답 (Login Token)
│   │   │   ├── ExchangeRequest.cs    # 토큰 교환 요청
│   │   │   └── TokenResponse.cs      # Access + Refresh Token 응답
│   │   ├── User/
│   │   │   ├── User.cs               # 사용자 정보
│   │   │   ├── UserListResponse.cs   # 사용자 목록 응답 (페이지네이션)
│   │   │   ├── LockUserRequest.cs    # 계정 잠금/해제 요청
│   │   │   └── ResetPasswordRequest.cs
│   │   └── Statistics/
│   │       └── ServerStatistics.cs   # 서버 통계 데이터
│   │
│   ├── ViewModels/                   # MVVM ViewModels
│   │   ├── Base/
│   │   │   └── ViewModelBase.cs      # ObservableObject 기반 베이스
│   │   ├── LoginViewModel.cs         # 로그인 화면
│   │   ├── MainViewModel.cs          # 메인 윈도우 (네비게이션)
│   │   ├── DashboardViewModel.cs     # 대시보드
│   │   ├── UserListViewModel.cs      # 사용자 목록
│   │   └── UserDetailViewModel.cs    # 사용자 상세/관리
│   │
│   ├── Views/                        # XAML Views
│   │   ├── LoginWindow.xaml          # 로그인 창
│   │   ├── Pages/
│   │   │   ├── DashboardPage.xaml    # 대시보드 페이지
│   │   │   ├── UserListPage.xaml     # 사용자 목록 페이지
│   │   │   └── UserDetailPage.xaml   # 사용자 상세 페이지
│   │   └── Controls/
│   │       ├── StatisticsCard.xaml   # 통계 카드 UserControl
│   │       └── UserActionPanel.xaml  # 사용자 관리 액션 패널
│   │
│   ├── Services/                     # 비즈니스 로직 & API 통신
│   │   ├── Auth/
│   │   │   ├── IAuthService.cs
│   │   │   └── AuthService.cs        # 관리자 인증 (Login → Exchange)
│   │   ├── User/
│   │   │   ├── IUserService.cs
│   │   │   └── UserService.cs        # 사용자 관리 API 호출
│   │   ├── Statistics/
│   │   │   ├── IStatisticsService.cs
│   │   │   └── StatisticsService.cs  # 통계 API 호출
│   │   └── Navigation/
│   │       ├── INavigationService.cs
│   │       └── NavigationService.cs  # 페이지 네비게이션
│   │
│   ├── Infrastructure/               # 인프라 레이어
│   │   ├── Http/
│   │   │   ├── AuthenticatedHttpClient.cs  # Access Token 자동 주입
│   │   │   └── TokenRefreshHandler.cs      # 401 시 Refresh Token으로 재시도
│   │   ├── Token/
│   │   │   ├── ITokenManager.cs
│   │   │   └── TokenManager.cs             # 토큰 메모리 저장/관리
│   │   └── Config/
│   │       └── AppSettings.cs              # API Base URL 등 설정
│   │
│   ├── Converters/                   # XAML Value Converters
│   │   ├── BoolToVisibilityConverter.cs
│   │   └── DateTimeFormatConverter.cs
│   │
│   └── Resources/                    # 리소스
│       ├── Styles/
│       │   └── CustomStyles.xaml     # ModernWpfUI 커스텀 스타일
│       └── Images/
│           └── logo.png
│
└── GMTool.Tests/                     # 단위 테스트
    └── Services/
        └── AuthServiceTests.cs
```

---

## 필수 NuGet 패키지

```xml
<!-- UI 프레임워크 -->
<PackageReference Include="ModernWpfUI" Version="0.9.6" />

<!-- HTTP 통신 -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="System.Net.Http" Version="4.3.4" />

<!-- MVVM 패턴 -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />

<!-- 의존성 주입 -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />

<!-- 테스트 -->
<PackageReference Include="xunit" Version="2.6.2" />
<PackageReference Include="Moq" Version="4.20.70" />
```

---

## 핵심 기능 구현 계획

### 1. 인증 시스템

#### 인증 플로우
```
1. 사용자 입력 (username, password)
   ↓
2. POST /api/admin/login → Login Token 획득 (1분 유효)
   ↓
3. POST /api/admin/exchange → Access Token (15분) + Refresh Token (1일)
   ↓
4. TokenManager에 토큰 저장 (메모리)
   ↓
5. MainWindow 표시, LoginWindow 닫기
```

#### 자동 토큰 갱신 (TokenRefreshHandler)
```csharp
// API 호출 → 401 Unauthorized 발생 시:
1. Refresh Token으로 POST /api/admin/refresh 호출
2. 새로운 Access Token 획득
3. TokenManager 업데이트
4. 원래 요청 재시도
5. Refresh Token 만료 시 → 로그아웃 후 LoginWindow로 이동
```

---

### 2. 주요 화면 설계

#### A. LoginWindow
**기능:**
- Username, Password 입력
- "로그인" 버튼 → AuthService.LoginAsync()
- 로딩 인디케이터 (ProgressRing)
- 에러 메시지 표시

**바인딩:**
```csharp
public class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string username;

    [ObservableProperty]
    private string password;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage;

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // 1. Login API → Login Token
            var loginToken = await _authService.LoginAsync(Username, Password);

            // 2. Exchange API → Access + Refresh Token
            await _authService.ExchangeTokenAsync(loginToken);

            // 3. MainWindow로 이동
            _navigationService.NavigateToMain();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

---

#### B. DashboardPage
**기능:**
- 서버 통계 카드 표시
  - 총 사용자 수
  - 활성 사용자 수
  - 잠긴 사용자 수
  - 현재 온라인 사용자 수
  - 오늘 가입 수
  - 오늘 로그인 수
- "새로고침" 버튼

**XAML 구조 (ModernWpfUI):**
```xml
<Page>
    <ScrollViewer>
        <ui:SimpleStackPanel Spacing="16" Margin="24">
            <TextBlock Text="대시보드" Style="{StaticResource TitleTextBlockStyle}" />

            <UniformGrid Rows="2" Columns="3">
                <local:StatisticsCard Title="총 사용자"
                                      Value="{Binding Statistics.TotalUsers}"
                                      Icon="People" />
                <local:StatisticsCard Title="활성 사용자"
                                      Value="{Binding Statistics.ActiveUsers}"
                                      Icon="CheckCircle" />
                <local:StatisticsCard Title="잠긴 계정"
                                      Value="{Binding Statistics.LockedUsers}"
                                      Icon="Lock" />
                <!-- ... -->
            </UniformGrid>

            <Button Content="새로고침"
                    Command="{Binding RefreshCommand}"
                    Style="{StaticResource AccentButtonStyle}" />
        </ui:SimpleStackPanel>
    </ScrollViewer>
</Page>
```

---

#### C. UserListPage
**기능:**
- 사용자 목록 DataGrid (페이지네이션)
- 검색 (username, email)
- 필터 (활성/비활성)
- 사용자 클릭 → UserDetailPage로 네비게이션

**ViewModel:**
```csharp
public class UserListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<User> users;

    [ObservableProperty]
    private string searchText;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages;

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        var response = await _userService.GetUsersAsync(
            page: CurrentPage,
            pageSize: 20,
            search: SearchText
        );

        Users = new ObservableCollection<User>(response.Users);
        TotalPages = response.TotalPages;
    }

    [RelayCommand]
    private void NavigateToUserDetail(int userId)
    {
        _navigationService.NavigateToUserDetail(userId);
    }
}
```

**XAML:**
```xml
<Page>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- 검색 바 -->
        <ui:AutoSuggestBox Grid.Row="0"
                           PlaceholderText="사용자 검색..."
                           Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />

        <!-- 사용자 목록 -->
        <DataGrid Grid.Row="1"
                  ItemsSource="{Binding Users}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True">
            <DataGrid.Columns>
                <DataGridTextColumn Header="ID" Binding="{Binding Id}" />
                <DataGridTextColumn Header="사용자명" Binding="{Binding Username}" />
                <DataGridTextColumn Header="이메일" Binding="{Binding Email}" />
                <DataGridTextColumn Header="상태" Binding="{Binding IsActive}" />
                <DataGridTextColumn Header="마지막 로그인" Binding="{Binding LastLoginAt}" />
            </DataGrid.Columns>
        </DataGrid>

        <!-- 페이지네이션 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center">
            <Button Content="이전" Command="{Binding PreviousPageCommand}" />
            <TextBlock Text="{Binding CurrentPage}" Margin="10,0" />
            <TextBlock Text="/" Margin="0,0,10,0" />
            <TextBlock Text="{Binding TotalPages}" />
            <Button Content="다음" Command="{Binding NextPageCommand}" />
        </StackPanel>
    </Grid>
</Page>
```

---

#### D. UserDetailPage
**기능:**
- 사용자 정보 표시
- 계정 잠금/해제 버튼
- 비밀번호 초기화
- 세션 강제 종료
- 사용자 삭제

**ViewModel:**
```csharp
public class UserDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private User user;

    [RelayCommand]
    private async Task LockUserAsync()
    {
        var result = MessageBox.Show(
            "이 사용자를 30분간 잠그시겠습니까?",
            "계정 잠금",
            MessageBoxButton.YesNo
        );

        if (result == MessageBoxResult.Yes)
        {
            await _userService.LockUserAsync(User.Id, durationMinutes: 30);
            await LoadUserAsync(); // 새로고침
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        // 비밀번호 입력 다이얼로그 표시
        var dialog = new PasswordResetDialog();
        if (dialog.ShowDialog() == true)
        {
            await _userService.ResetPasswordAsync(User.Id, dialog.NewPassword);
            MessageBox.Show("비밀번호가 초기화되었습니다.");
        }
    }

    [RelayCommand]
    private async Task TerminateSessionsAsync()
    {
        await _userService.TerminateSessionsAsync(User.Id);
        MessageBox.Show("모든 세션이 종료되었습니다.");
    }
}
```

---

### 3. 인프라 레이어 상세 설계

#### TokenManager
```csharp
public interface ITokenManager
{
    string? AccessToken { get; }
    string? RefreshToken { get; }

    void SetTokens(string accessToken, string refreshToken);
    void ClearTokens();
    bool HasValidTokens();
}

public class TokenManager : ITokenManager
{
    private string? _accessToken;
    private string? _refreshToken;

    public string? AccessToken => _accessToken;
    public string? RefreshToken => _refreshToken;

    public void SetTokens(string accessToken, string refreshToken)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
    }

    public void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
    }

    public bool HasValidTokens()
    {
        return !string.IsNullOrEmpty(_accessToken) &&
               !string.IsNullOrEmpty(_refreshToken);
    }
}
```

#### TokenRefreshHandler (DelegatingHandler)
```csharp
public class TokenRefreshHandler : DelegatingHandler
{
    private readonly ITokenManager _tokenManager;
    private readonly IAuthService _authService;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 1. Access Token 헤더 추가
        if (_tokenManager.AccessToken != null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);
        }

        // 2. 요청 전송
        var response = await base.SendAsync(request, cancellationToken);

        // 3. 401 Unauthorized 처리
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Refresh Token으로 갱신 시도
            var refreshSuccess = await _authService.RefreshTokenAsync();

            if (refreshSuccess)
            {
                // 새로운 Access Token으로 재시도
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);
                response = await base.SendAsync(request, cancellationToken);
            }
            else
            {
                // Refresh Token 만료 → 로그아웃
                _tokenManager.ClearTokens();
                // TODO: LoginWindow로 네비게이션
            }
        }

        return response;
    }
}
```

#### AuthService
```csharp
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenManager _tokenManager;
    private const string BaseUrl = "http://localhost:5000/api/admin";

    public async Task<string> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password,
            DeviceId = "GMTool_Desktop"
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/login", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsAsync<LoginResponse>();
        return result.Token; // Login Token
    }

    public async Task ExchangeTokenAsync(string loginToken)
    {
        var request = new ExchangeRequest
        {
            LoginToken = loginToken,
            DeviceId = "GMTool_Desktop"
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/exchange", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsAsync<TokenResponse>();
        _tokenManager.SetTokens(result.AccessToken, result.RefreshToken);
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (_tokenManager.RefreshToken == null)
            return false;

        try
        {
            var request = new RefreshRequest
            {
                RefreshToken = _tokenManager.RefreshToken,
                DeviceId = "GMTool_Desktop"
            };

            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/refresh", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<TokenResponse>();
            _tokenManager.SetTokens(result.Token, _tokenManager.RefreshToken);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

---

### 4. 의존성 주입 설정 (App.xaml.cs)

```csharp
public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<ITokenManager, TokenManager>();
        services.AddTransient<TokenRefreshHandler>();

        // HttpClient with TokenRefreshHandler
        services.AddHttpClient<IAuthService, AuthService>()
            .AddHttpMessageHandler<TokenRefreshHandler>();
        services.AddHttpClient<IUserService, UserService>()
            .AddHttpMessageHandler<TokenRefreshHandler>();
        services.AddHttpClient<IStatisticsService, StatisticsService>()
            .AddHttpMessageHandler<TokenRefreshHandler>();

        // Services
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UserListViewModel>();
        services.AddTransient<UserDetailViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddSingleton<MainWindow>();
    }
}
```

---

## 개발 순서

### Phase 1: 기본 인프라
1. WPF 프로젝트 생성 (`dotnet new wpf`)
2. NuGet 패키지 설치
3. Models 작성 (DTOs)
4. TokenManager 구현
5. DI 컨테이너 설정

### Phase 2: 인증 시스템
1. AuthService 구현
2. TokenRefreshHandler 구현
3. LoginWindow + LoginViewModel
4. 로그인 플로우 테스트

### Phase 3: 메인 기능
1. MainWindow + NavigationService
2. DashboardPage + StatisticsService
3. UserListPage + UserService
4. UserDetailPage + 관리 기능

### Phase 4: UI 개선
1. ModernWpfUI 스타일 적용
2. 로딩 인디케이터
3. 에러 처리 개선
4. 애니메이션 추가

---

## 보안 고려사항

### 토큰 저장
- ✅ **Access Token**: 메모리 (TokenManager)
- ✅ **Refresh Token**: 메모리 (TokenManager)
- ❌ LocalStorage, 파일 저장 금지

### HTTPS
- 프로덕션 환경에서는 반드시 HTTPS 사용
- 개발 환경: `http://localhost:5000` (임시)
- 프로덕션: `https://your-domain.com`

### 민감 정보
- 비밀번호는 PasswordBox 사용 (메모리 보호)
- 로그에 토큰 출력 금지

---

## 추가 기능 아이디어 (선택)

- [ ] 사용자 활동 로그 조회
- [ ] 실시간 통계 자동 갱신 (SignalR)
- [ ] 다크 모드 지원
- [ ] 설정 페이지 (API URL 변경 등)
- [ ] Excel 내보내기 (사용자 목록)
- [ ] 알림 시스템 (Toast Notification)

---

## 프로젝트 생성 명령어

```bash
# 솔루션 생성
dotnet new sln -n GMTool

# WPF 프로젝트 생성
dotnet new wpf -n GMTool -f net8.0-windows

# 솔루션에 프로젝트 추가
dotnet sln add GMTool/GMTool.csproj

# NuGet 패키지 설치
cd GMTool
dotnet add package ModernWpfUI
dotnet add package Newtonsoft.Json
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Http
```

---

## 참고 자료

- [ModernWpfUI GitHub](https://github.com/Kinnara/ModernWpf)
- [CommunityToolkit.Mvvm Docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [ASP.NET Core AuthServer API 문서](./API_DOCUMENTATION.md)
