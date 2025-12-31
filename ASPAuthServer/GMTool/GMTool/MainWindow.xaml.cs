using GMTool.Services.Navigation;
using GMTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GMTool;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly INavigationService _navigationService;

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _navigationService = navigationService;
        DataContext = _viewModel;

        // NavigationService에 Frame 연결
        _navigationService.SetFrame(MainFrame);

        // Logout 이벤트 핸들러
        _viewModel.LogoutRequested += OnLogoutRequested;

        // 기본 페이지로 대시보드 표시
        _navigationService.NavigateTo("Dashboard");
    }

    private void OnLogoutRequested(object? sender, System.EventArgs e)
    {
        // 로그아웃 처리 - LoginWindow로 돌아가기
        var loginWindow = App.ServiceProvider?.GetService<Views.LoginWindow>();
        if (loginWindow != null)
        {
            loginWindow.Show();
            this.Close();
        }
    }
}