using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMTool.Services.Auth;
using GMTool.Services.Logging;
using System;
using System.Threading.Tasks;

namespace GMTool.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ILogService _logService;

        public event EventHandler? LoginSucceeded;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _rememberDevice;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(IAuthService authService, ILogService logService)
        {
            _authService = authService;
            _logService = logService;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(Username))
            {
                ShowError("사용자명을 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowError("비밀번호를 입력하세요.");
                return;
            }

            // UI 상태 업데이트
            IsLoading = true;
            HideError();

            try
            {
                _logService.Info($"로그인 시도: {Username}");

                // 로그인 시도
                var token = await _authService.LoginAsync(Username, Password);

                if (string.IsNullOrEmpty(token))
                {
                    _logService.Error("로그인 실패: 인증 정보가 올바르지 않습니다.");
                    ShowError("로그인 실패. 사용자명 또는 비밀번호를 확인하세요.");
                    return;
                }

                _logService.Success($"로그인 성공: {Username}");

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
                IsLoading = false;
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private void HideError()
        {
            HasError = false;
            ErrorMessage = string.Empty;
        }
    }
}
