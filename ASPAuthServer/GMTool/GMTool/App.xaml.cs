using GMTool.Infrastructure.Config;
using GMTool.Infrastructure.Http;
using GMTool.Infrastructure.Token;
using GMTool.Services.Auth;
using GMTool.Services.Logging;
using GMTool.Services.Navigation;
using GMTool.Services.Statistics;
using GMTool.Services.User;
using GMTool.ViewModels;
using GMTool.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace GMTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public static IServiceProvider? ServiceProvider { get; private set; }

    public IServiceProvider Services => _serviceProvider
        ?? throw new InvalidOperationException("ServiceProvider가 초기화되지 않았습니다.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 의존성 주입 컨테이너 설정
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            ServiceProvider = _serviceProvider;

            // LoginWindow 표시
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();

            // 로그인 성공 시 MainWindow 열기
            loginWindow.LoginSucceeded += (sender, args) =>
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                loginWindow.Close();
            };

            loginWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"애플리케이션 시작 실패:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // ========== Infrastructure ==========

        // AppSettings (싱글톤)
        services.AddSingleton(new AppSettings
        {
            ApiBaseUrl = "http://localhost:5126",
            DeviceId = "GMTool_Desktop",
            MaxLogCount = 500,
            RequestTimeoutSeconds = 30
        });

        // TokenManager (싱글톤)
        services.AddSingleton<ITokenManager, TokenManager>();

        // TokenRefreshHandler (Transient)
        services.AddTransient<TokenRefreshHandler>();

        // ========== Services ==========

        // LogService (싱글톤 - 모든 곳에서 같은 로그 인스턴스 사용)
        services.AddSingleton<ILogService, LogService>();

        // NavigationService (싱글톤)
        services.AddSingleton<INavigationService, NavigationService>();

        // HttpClient with TokenRefreshHandler
        services.AddHttpClient<IAuthService, AuthService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var settings = sp.GetRequiredService<AppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            })
            .AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IUserService, UserService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var settings = sp.GetRequiredService<AppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            })
            .AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IStatisticsService, StatisticsService>()
            .ConfigureHttpClient((sp, client) =>
            {
                var settings = sp.GetRequiredService<AppSettings>();
                client.BaseAddress = new Uri(settings.ApiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            })
            .AddHttpMessageHandler<TokenRefreshHandler>();

        // ========== ViewModels ==========
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UserListViewModel>();
        services.AddTransient<MainWindowViewModel>();

        // ========== Views ==========

        // Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();

        // Pages
        services.AddTransient<Views.Pages.DashboardPage>();
        services.AddTransient<Views.Pages.UserListPage>();
        services.AddTransient<Views.Pages.SettingsPage>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // DI 컨테이너 정리
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}

