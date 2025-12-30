using GMTool.Services.Auth;
using GMTool.Services.Logging;
using System;
using System.Windows;

namespace GMTool.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IAuthService _authService;
        private readonly ILogService _logService;

        public event EventHandler? LoginSucceeded;

        public LoginWindow(IAuthService authService, ILogService logService)
        {
            InitializeComponent();

            _authService = authService;
            _logService = logService;

            // 로그인 버튼 클릭 이벤트
            LoginButton.Click += LoginButton_Click;

            // Enter 키로 로그인
            PasswordBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    LoginButton_Click(this, new RoutedEventArgs());
                }
            };
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            // 유효성 검사
            if (string.IsNullOrEmpty(username))
            {
                ShowError("사용자명을 입력하세요.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("비밀번호를 입력하세요.");
                return;
            }

            // UI 업데이트
            SetLoading(true);
            HideError();

            try
            {
                _logService.Info($"로그인 시도: {username}");

                // 로그인 시도
                var token = await _authService.LoginAsync(username, password);

                if (string.IsNullOrEmpty(token))
                {
                    _logService.Error("로그인 실패: 인증 정보가 올바르지 않습니다.");
                    ShowError("로그인 실패. 사용자명 또는 비밀번호를 확인하세요.");
                }

                _logService.Success($"로그인 성공: {username}");

                // 접속 토큰 교환
                await _authService.ExchangeTokenAsync(token);

                // 로그인 성공 이벤트 발생
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logService.Error($"로그인 오류: {ex.Message}");
                ShowError($"로그인 중 오류가 발생했습니다: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool isLoading)
        {
            LoginButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            LoadingRing.IsActive = isLoading;
            LoadingRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorMessageBorder.Visibility = Visibility.Collapsed;
        }
    }
}
