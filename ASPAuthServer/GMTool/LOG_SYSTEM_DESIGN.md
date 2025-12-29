# GM Tool - 로그 시스템 설계

## 개요
모든 페이지에서 하단에 고정된 로그 뷰어를 제공하여 API 호출, 에러, 사용자 액션 등을 실시간으로 확인할 수 있는 시스템

---

## UI 레이아웃 구조

### LoginWindow (로그인 전)
```
┌─────────────────────────────────────┐
│         로그인 화면 (중앙)            │
│                                     │
│     [Username]                      │
│     [Password]                      │
│     [로그인 버튼]                     │
│                                     │
├─────────────────────────────────────┤
│  📝 로그 영역 (고정, 200px 높이)      │
│  [12:34:56] POST /api/admin/login   │
│  [12:34:57] ✅ 로그인 성공            │
└─────────────────────────────────────┘
```

### MainWindow (로그인 후)
```
┌─────────────────────────────────────┐
│  [사이드바]  │   페이지 콘텐츠         │
│  Dashboard  │   (DashboardPage,    │
│  사용자관리   │    UserListPage 등)   │
│  설정       │                       │
│            │                       │
├─────────────────────────────────────┤
│  📝 로그 영역 (고정, 200px 높이)      │
│  [🔍] 검색   [🗑️] 클리어   [레벨▼]   │
│  [12:35:10] GET /api/admin/users   │
│  [12:35:11] ✅ 사용자 목록 로드 (50건)│
│  [12:35:20] ⚠️ 토큰 갱신 필요         │
└─────────────────────────────────────┘
```

---

## 프로젝트 구조 추가 사항

```
GMTool/
├── Services/
│   └── Logging/
│       ├── ILogService.cs           # 로그 서비스 인터페이스
│       ├── LogService.cs            # 싱글톤 로그 서비스
│       └── LogEntry.cs              # 로그 항목 모델
│
├── ViewModels/
│   └── LogViewModel.cs              # 로그 뷰어 ViewModel (싱글톤)
│
└── Views/
    └── Controls/
        └── LogViewer.xaml           # 로그 뷰어 UserControl
```

---

## 1. 로그 모델 설계

### LogEntry.cs
```csharp
using System;

namespace GMTool.Services.Logging
{
    public enum LogLevel
    {
        Debug,    // 🔍 디버그 (회색)
        Info,     // ℹ️ 정보 (파란색)
        Success,  // ✅ 성공 (초록색)
        Warning,  // ⚠️ 경고 (주황색)
        Error     // ❌ 에러 (빨간색)
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string? Details { get; set; }  // 상세 정보 (선택)

        public LogEntry(LogLevel level, string message, string? details = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Details = details;
        }

        // UI 표시용 포맷
        public string FormattedMessage =>
            $"[{Timestamp:HH:mm:ss}] {GetLevelIcon()} {Message}";

        private string GetLevelIcon() => Level switch
        {
            LogLevel.Debug => "🔍",
            LogLevel.Info => "ℹ️",
            LogLevel.Success => "✅",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            _ => ""
        };
    }
}
```

---

## 2. 로그 서비스 (싱글톤)

### ILogService.cs
```csharp
using System;
using System.Collections.ObjectModel;

namespace GMTool.Services.Logging
{
    public interface ILogService
    {
        ObservableCollection<LogEntry> Logs { get; }

        void Debug(string message, string? details = null);
        void Info(string message, string? details = null);
        void Success(string message, string? details = null);
        void Warning(string message, string? details = null);
        void Error(string message, string? details = null);
        void Error(Exception ex, string message);

        void Clear();
    }
}
```

### LogService.cs
```csharp
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace GMTool.Services.Logging
{
    public class LogService : ILogService
    {
        private const int MAX_LOG_COUNT = 500;  // 최대 500개 유지 (성능)

        public ObservableCollection<LogEntry> Logs { get; }

        public LogService()
        {
            Logs = new ObservableCollection<LogEntry>();
        }

        public void Debug(string message, string? details = null)
        {
            AddLog(LogLevel.Debug, message, details);
        }

        public void Info(string message, string? details = null)
        {
            AddLog(LogLevel.Info, message, details);
        }

        public void Success(string message, string? details = null)
        {
            AddLog(LogLevel.Success, message, details);
        }

        public void Warning(string message, string? details = null)
        {
            AddLog(LogLevel.Warning, message, details);
        }

        public void Error(string message, string? details = null)
        {
            AddLog(LogLevel.Error, message, details);
        }

        public void Error(Exception ex, string message)
        {
            var details = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            AddLog(LogLevel.Error, message, details);
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
            });
        }

        private void AddLog(LogLevel level, string message, string? details = null)
        {
            var logEntry = new LogEntry(level, message, details);

            // UI 스레드에서 실행
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, logEntry);  // 최신 로그가 위로

                // 최대 개수 제한 (성능)
                while (Logs.Count > MAX_LOG_COUNT)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            });
        }
    }
}
```

---

## 3. 로그 ViewModel

### LogViewModel.cs
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMTool.Services.Logging;
using System.Collections.ObjectModel;
using System.Linq;

namespace GMTool.ViewModels
{
    public partial class LogViewModel : ObservableObject
    {
        private readonly ILogService _logService;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private LogLevel? selectedLogLevel;  // null = 전체

        [ObservableProperty]
        private ObservableCollection<LogEntry> filteredLogs;

        public LogViewModel(ILogService logService)
        {
            _logService = logService;
            FilteredLogs = _logService.Logs;
        }

        [RelayCommand]
        private void ClearLogs()
        {
            _logService.Clear();
        }

        [RelayCommand]
        private void FilterLogs()
        {
            var query = _logService.Logs.AsEnumerable();

            // 로그 레벨 필터
            if (selectedLogLevel.HasValue)
            {
                query = query.Where(log => log.Level == selectedLogLevel.Value);
            }

            // 검색어 필터
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(log =>
                    log.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (log.Details != null && log.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            FilteredLogs = new ObservableCollection<LogEntry>(query);
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterLogs();
        }

        partial void OnSelectedLogLevelChanged(LogLevel? value)
        {
            FilterLogs();
        }
    }
}
```

---

## 4. 로그 뷰어 UserControl

### LogViewer.xaml
```xml
<UserControl x:Class="GMTool.Views.Controls.LogViewer"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.modernwpf.com/2019"
             Height="200">

    <UserControl.Resources>
        <!-- 로그 레벨별 색상 -->
        <SolidColorBrush x:Key="DebugBrush" Color="#9E9E9E" />
        <SolidColorBrush x:Key="InfoBrush" Color="#2196F3" />
        <SolidColorBrush x:Key="SuccessBrush" Color="#4CAF50" />
        <SolidColorBrush x:Key="WarningBrush" Color="#FF9800" />
        <SolidColorBrush x:Key="ErrorBrush" Color="#F44336" />
    </UserControl.Resources>

    <Border Background="#F5F5F5" BorderBrush="#E0E0E0" BorderThickness="0,1,0,0">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="40" />   <!-- 툴바 -->
                <RowDefinition Height="*" />    <!-- 로그 목록 -->
            </Grid.RowDefinitions>

            <!-- 툴바 -->
            <Border Grid.Row="0" Background="White" BorderBrush="#E0E0E0" BorderThickness="0,0,0,1" Padding="8">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>

                    <!-- 검색 -->
                    <TextBox Grid.Column="0"
                             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                             ui:ControlHelper.PlaceholderText="로그 검색..."
                             VerticalAlignment="Center"
                             Margin="0,0,8,0" />

                    <!-- 로그 레벨 필터 -->
                    <ComboBox Grid.Column="1"
                              SelectedItem="{Binding SelectedLogLevel}"
                              VerticalAlignment="Center"
                              Width="100"
                              Margin="0,0,8,0">
                        <ComboBoxItem Content="전체" />
                        <ComboBoxItem Content="디버그" Tag="{x:Static local:LogLevel.Debug}" />
                        <ComboBoxItem Content="정보" Tag="{x:Static local:LogLevel.Info}" />
                        <ComboBoxItem Content="성공" Tag="{x:Static local:LogLevel.Success}" />
                        <ComboBoxItem Content="경고" Tag="{x:Static local:LogLevel.Warning}" />
                        <ComboBoxItem Content="에러" Tag="{x:Static local:LogLevel.Error}" />
                    </ComboBox>

                    <!-- 클리어 버튼 -->
                    <Button Grid.Column="2"
                            Content="🗑️ 클리어"
                            Command="{Binding ClearLogsCommand}"
                            VerticalAlignment="Center"
                            Margin="0,0,8,0" />

                    <!-- 로그 개수 -->
                    <TextBlock Grid.Column="3"
                               Text="{Binding FilteredLogs.Count, StringFormat='총 {0}개'}"
                               VerticalAlignment="Center"
                               Foreground="#666"
                               Margin="8,0,0,0" />
                </Grid>
            </Border>

            <!-- 로그 목록 -->
            <ListBox Grid.Row="1"
                     ItemsSource="{Binding FilteredLogs}"
                     Background="White"
                     BorderThickness="0"
                     ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                     VirtualizingPanel.IsVirtualizing="True"
                     VirtualizingPanel.VirtualizationMode="Recycling">

                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Margin="4,2">
                            <!-- 로그 메시지 -->
                            <TextBlock Text="{Binding FormattedMessage}"
                                       FontFamily="Consolas"
                                       FontSize="12"
                                       TextWrapping="Wrap">
                                <TextBlock.Style>
                                    <Style TargetType="TextBlock">
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding Level}" Value="{x:Static local:LogLevel.Debug}">
                                                <Setter Property="Foreground" Value="{StaticResource DebugBrush}" />
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding Level}" Value="{x:Static local:LogLevel.Info}">
                                                <Setter Property="Foreground" Value="{StaticResource InfoBrush}" />
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding Level}" Value="{x:Static local:LogLevel.Success}">
                                                <Setter Property="Foreground" Value="{StaticResource SuccessBrush}" />
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding Level}" Value="{x:Static local:LogLevel.Warning}">
                                                <Setter Property="Foreground" Value="{StaticResource WarningBrush}" />
                                            </DataTrigger>
                                            <DataTrigger Binding="{Binding Level}" Value="{x:Static local:LogLevel.Error}">
                                                <Setter Property="Foreground" Value="{StaticResource ErrorBrush}" />
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </TextBlock.Style>
                            </TextBlock>

                            <!-- 상세 정보 (있을 경우) -->
                            <TextBlock Text="{Binding Details}"
                                       FontFamily="Consolas"
                                       FontSize="11"
                                       Foreground="#999"
                                       TextWrapping="Wrap"
                                       Margin="20,2,0,0"
                                       Visibility="{Binding Details, Converter={StaticResource NullToVisibilityConverter}}" />
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>

                <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                        <Setter Property="Padding" Value="8,4" />
                    </Style>
                </ListBox.ItemContainerStyle>
            </ListBox>
        </Grid>
    </Border>
</UserControl>
```

### LogViewer.xaml.cs
```csharp
using GMTool.ViewModels;
using System.Windows.Controls;

namespace GMTool.Views.Controls
{
    public partial class LogViewer : UserControl
    {
        public LogViewer()
        {
            InitializeComponent();

            // DI에서 주입받은 LogViewModel 사용
            DataContext = App.Current.Services.GetService<LogViewModel>();
        }
    }
}
```

---

## 5. LoginWindow 레이아웃 (로그 포함)

### LoginWindow.xaml
```xml
<Window x:Class="GMTool.Views.LoginWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.modernwpf.com/2019"
        xmlns:controls="clr-namespace:GMTool.Views.Controls"
        Title="GM Tool - 로그인"
        Width="600"
        Height="500"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanResize">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />      <!-- 로그인 영역 -->
            <RowDefinition Height="200" />    <!-- 로그 영역 (고정) -->
        </Grid.RowDefinitions>

        <!-- 로그인 영역 -->
        <Border Grid.Row="0" Background="White">
            <StackPanel VerticalAlignment="Center"
                        HorizontalAlignment="Center"
                        Width="300">

                <TextBlock Text="🎮 GM Tool"
                           FontSize="28"
                           FontWeight="SemiBold"
                           HorizontalAlignment="Center"
                           Margin="0,0,0,32" />

                <!-- Username -->
                <TextBlock Text="사용자명" Margin="0,0,0,4" />
                <TextBox Text="{Binding Username, UpdateSourceTrigger=PropertyChanged}"
                         Margin="0,0,0,16" />

                <!-- Password -->
                <TextBlock Text="비밀번호" Margin="0,0,0,4" />
                <PasswordBox x:Name="PasswordBox"
                             Margin="0,0,0,16" />

                <!-- 에러 메시지 -->
                <TextBlock Text="{Binding ErrorMessage}"
                           Foreground="Red"
                           TextWrapping="Wrap"
                           Margin="0,0,0,16"
                           Visibility="{Binding ErrorMessage, Converter={StaticResource NullToVisibilityConverter}}" />

                <!-- 로그인 버튼 -->
                <Button Content="로그인"
                        Command="{Binding LoginCommand}"
                        CommandParameter="{Binding ElementName=PasswordBox}"
                        IsEnabled="{Binding IsLoading, Converter={StaticResource InverseBooleanConverter}}"
                        Height="36"
                        FontSize="14" />

                <!-- 로딩 -->
                <ui:ProgressRing IsActive="{Binding IsLoading}"
                                 Width="32"
                                 Height="32"
                                 Margin="0,16,0,0" />
            </StackPanel>
        </Border>

        <!-- 로그 뷰어 (하단 고정) -->
        <controls:LogViewer Grid.Row="1" />
    </Grid>
</Window>
```

---

## 6. MainWindow 레이아웃 (로그 포함)

### MainWindow.xaml
```xml
<Window x:Class="GMTool.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.modernwpf.com/2019"
        xmlns:controls="clr-namespace:GMTool.Views.Controls"
        Title="GM Tool"
        Width="1200"
        Height="800"
        WindowStartupLocation="CenterScreen">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />      <!-- 메인 콘텐츠 -->
            <RowDefinition Height="5" />      <!-- 리사이저 -->
            <RowDefinition Height="200" MinHeight="100" MaxHeight="400" /> <!-- 로그 영역 (리사이즈 가능) -->
        </Grid.RowDefinitions>

        <!-- 메인 콘텐츠 (사이드바 + 페이지) -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="200" />  <!-- 사이드바 -->
                <ColumnDefinition Width="*" />    <!-- 페이지 콘텐츠 -->
            </Grid.ColumnDefinitions>

            <!-- 사이드바 -->
            <Border Grid.Column="0" Background="#2C3E50" BorderBrush="#34495E" BorderThickness="0,0,1,0">
                <StackPanel Margin="0,16,0,0">
                    <Button Content="📊 대시보드"
                            Command="{Binding NavigateToDashboardCommand}"
                            Style="{StaticResource SidebarButtonStyle}" />

                    <Button Content="👥 사용자 관리"
                            Command="{Binding NavigateToUsersCommand}"
                            Style="{StaticResource SidebarButtonStyle}" />

                    <Button Content="⚙️ 설정"
                            Command="{Binding NavigateToSettingsCommand}"
                            Style="{StaticResource SidebarButtonStyle}" />

                    <Separator Margin="0,16" />

                    <Button Content="🚪 로그아웃"
                            Command="{Binding LogoutCommand}"
                            Style="{StaticResource SidebarButtonStyle}"
                            VerticalAlignment="Bottom" />
                </StackPanel>
            </Border>

            <!-- 페이지 콘텐츠 -->
            <Frame Grid.Column="1"
                   x:Name="MainFrame"
                   NavigationUIVisibility="Hidden"
                   Background="#FAFAFA" />
        </Grid>

        <!-- GridSplitter (로그 영역 높이 조절) -->
        <GridSplitter Grid.Row="1"
                      Height="5"
                      HorizontalAlignment="Stretch"
                      VerticalAlignment="Center"
                      Background="#E0E0E0"
                      Cursor="SizeNS" />

        <!-- 로그 뷰어 (하단 고정) -->
        <controls:LogViewer Grid.Row="2" />
    </Grid>
</Window>
```

---

## 7. 서비스에서 로그 사용 예시

### AuthService.cs
```csharp
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenManager _tokenManager;
    private readonly ILogService _logService;  // ✅ 로그 서비스 주입

    public AuthService(HttpClient httpClient, ITokenManager tokenManager, ILogService logService)
    {
        _httpClient = httpClient;
        _tokenManager = tokenManager;
        _logService = logService;
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        try
        {
            _logService.Info($"로그인 시도: {username}");  // ℹ️ 로그

            var request = new LoginRequest
            {
                Username = username,
                Password = password,
                DeviceId = "GMTool_Desktop"
            };

            var response = await _httpClient.PostAsJsonAsync("/api/admin/login", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logService.Error($"로그인 실패: {response.StatusCode}", errorContent);  // ❌
                throw new Exception("로그인 실패");
            }

            var result = await response.Content.ReadAsAsync<LoginResponse>();
            _logService.Success($"로그인 성공: {username}");  // ✅

            return result.Token;
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "로그인 중 예외 발생");  // ❌
            throw;
        }
    }

    public async Task ExchangeTokenAsync(string loginToken)
    {
        try
        {
            _logService.Info("토큰 교환 시작");

            var request = new ExchangeRequest
            {
                LoginToken = loginToken,
                DeviceId = "GMTool_Desktop"
            };

            var response = await _httpClient.PostAsJsonAsync("/api/admin/exchange", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<TokenResponse>();
            _tokenManager.SetTokens(result.AccessToken, result.RefreshToken);

            _logService.Success("Access Token 획득 완료");  // ✅
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "토큰 교환 실패");
            throw;
        }
    }
}
```

### UserService.cs
```csharp
public async Task<UserListResponse> GetUsersAsync(int page, int pageSize, string? search = null)
{
    try
    {
        _logService.Debug($"사용자 목록 요청: page={page}, pageSize={pageSize}, search={search}");

        var url = $"/api/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(search))
            url += $"&search={search}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsAsync<UserListResponse>();

        _logService.Success($"사용자 목록 로드 완료: {result.TotalCount}건");

        return result;
    }
    catch (Exception ex)
    {
        _logService.Error(ex, "사용자 목록 로드 실패");
        throw;
    }
}

public async Task LockUserAsync(int userId, int durationMinutes)
{
    try
    {
        _logService.Warning($"사용자 #{userId} 계정 잠금 시도: {durationMinutes}분");

        var request = new LockUserRequest { Lock = true, DurationMinutes = durationMinutes };
        var response = await _httpClient.PatchAsJsonAsync($"/api/admin/users/{userId}/lock", request);
        response.EnsureSuccessStatusCode();

        _logService.Success($"사용자 #{userId} 계정 잠금 완료");
    }
    catch (Exception ex)
    {
        _logService.Error(ex, $"사용자 #{userId} 계정 잠금 실패");
        throw;
    }
}
```

---

## 8. DI 설정 업데이트

### App.xaml.cs
```csharp
private void ConfigureServices(IServiceCollection services)
{
    // ✅ 로그 서비스 (싱글톤)
    services.AddSingleton<ILogService, LogService>();
    services.AddSingleton<LogViewModel>();  // LogViewModel도 싱글톤

    // Infrastructure
    services.AddSingleton<ITokenManager, TokenManager>();
    services.AddTransient<TokenRefreshHandler>();

    // HttpClient with LogService 주입
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

    // Views
    services.AddTransient<LoginWindow>();
    services.AddSingleton<MainWindow>();
}
```

---

## 9. TokenRefreshHandler에서 로그 추가

```csharp
public class TokenRefreshHandler : DelegatingHandler
{
    private readonly ITokenManager _tokenManager;
    private readonly IAuthService _authService;
    private readonly ILogService _logService;  // ✅ 로그 서비스

    public TokenRefreshHandler(ITokenManager tokenManager, IAuthService authService, ILogService logService)
    {
        _tokenManager = tokenManager;
        _authService = authService;
        _logService = logService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestRequest request,
        CancellationToken cancellationToken)
    {
        // 요청 로그
        _logService.Debug($"{request.Method} {request.RequestUri?.PathAndQuery}");

        if (_tokenManager.AccessToken != null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // 응답 로그
        if (response.IsSuccessStatusCode)
        {
            _logService.Debug($"✅ {request.Method} {request.RequestUri?.PathAndQuery} → {response.StatusCode}");
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logService.Warning("⚠️ Access Token 만료, 갱신 시도 중...");

            var refreshSuccess = await _authService.RefreshTokenAsync();

            if (refreshSuccess)
            {
                _logService.Success("Access Token 갱신 성공");
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);
                response = await base.SendAsync(request, cancellationToken);
            }
            else
            {
                _logService.Error("Refresh Token 만료, 재로그인 필요");
                _tokenManager.ClearTokens();
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logService.Error($"❌ {request.Method} {request.RequestUri?.PathAndQuery} → {response.StatusCode}", errorContent);
        }

        return response;
    }
}
```

---

## 10. 로그 활용 예시

### 로그인 플로우
```
[12:34:56] ℹ️ 로그인 시도: admin
[12:34:57] 🔍 POST /api/admin/login
[12:34:57] 🔍 ✅ POST /api/admin/login → OK
[12:34:57] ✅ 로그인 성공: admin
[12:34:57] ℹ️ 토큰 교환 시작
[12:34:57] 🔍 POST /api/admin/exchange
[12:34:58] ✅ Access Token 획득 완료
```

### 사용자 관리
```
[12:35:10] 🔍 사용자 목록 요청: page=1, pageSize=20, search=null
[12:35:10] 🔍 GET /api/admin/users?page=1&pageSize=20
[12:35:11] ✅ 사용자 목록 로드 완료: 150건
[12:35:20] ⚠️ 사용자 #42 계정 잠금 시도: 30분
[12:35:20] 🔍 PATCH /api/admin/users/42/lock
[12:35:21] ✅ 사용자 #42 계정 잠금 완료
```

### 토큰 갱신
```
[12:50:30] 🔍 GET /api/admin/users?page=1&pageSize=20
[12:50:30] ⚠️ Access Token 만료, 갱신 시도 중...
[12:50:31] ✅ Access Token 갱신 성공
[12:50:31] 🔍 ✅ GET /api/admin/users?page=1&pageSize=20 → OK
```

---

## 성능 최적화

### 1. 로그 개수 제한
```csharp
private const int MAX_LOG_COUNT = 500;  // 최대 500개만 유지
```

### 2. UI 가상화
```xml
<ListBox VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
```

### 3. 필터링 성능
```csharp
// 검색 시 Debounce 적용 (선택사항)
private Timer _filterDebounceTimer;

partial void OnSearchTextChanged(string value)
{
    _filterDebounceTimer?.Dispose();
    _filterDebounceTimer = new Timer(300);  // 300ms 후 필터
    _filterDebounceTimer.Elapsed += (s, e) => FilterLogs();
    _filterDebounceTimer.Start();
}
```

---

## 추가 기능 아이디어

- [ ] 로그 파일 저장 (txt, csv)
- [ ] 로그 자동 스크롤 (최신 로그로)
- [ ] 더블 클릭 시 상세 정보 다이얼로그
- [ ] 특정 로그 레벨 강조 (Highlight)
- [ ] 로그 통계 (에러 개수, 경고 개수 등)
- [ ] 로그 북마크 기능
