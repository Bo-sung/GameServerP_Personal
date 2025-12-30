using GMTool.Infrastructure.Token;
using GMTool.Services.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GMTool.Services.Auth
{
    public record LoginRequest(
        string Username,
        string Password,
        string? DeviceId
    );

    public record ExchangeRequest(
        string LoginToken,
        string? DeviceId
    );

    public record RefreshRequest(
        string RefreshToken,
        string? DeviceId
    );

    public record LogoutRequest(
        string DeviceId
    );

    public record LoginResponse(
        string Token,
        long? ExpiresIn = null
    );

    public record ExchangeResponse(
        string AccessToken,
        string RefreshToken
    );

    public record RefreshResponse(
        string Message,
        string AccToken
    );

    public record LogoutResponse(
        string Message
    );
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenManager _tokenManager;
        private readonly ILogService _logService;

        public AuthService(
            HttpClient httpClient,
            ITokenManager tokenManager,
            ILogService logService)
        {
            _httpClient = httpClient;
            _tokenManager = tokenManager;
            _logService = logService;
        }

        public async Task<string> LoginAsync(string username, string password, string deviceId = "GMTool_Desktop")
        {
            try
            {
                _logService.Info($"관리자 로그인 시도: {username}");

                var request = new LoginRequest(Username: username, Password: password, DeviceId: deviceId);


                var response = await _httpClient.PostAsJsonAsync("/api/admin/login", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"로그인 실패: {response.StatusCode}", errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        throw new UnauthorizedAccessException("인증 실패: 사용자명 또는 비밀번호가 올바르지 않습니다.");

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        throw new UnauthorizedAccessException("권한 없음: 관리자 계정이 아닙니다.");

                    throw new HttpRequestException($"로그인 실패: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result == null || string.IsNullOrEmpty(result.Token))
                    throw new InvalidOperationException("로그인 응답이 유효하지 않습니다.");

                _logService.Success($"로그인 성공: {username}");

                return result.Token; // Login Token 반환
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "로그인 중 예외 발생");
                throw;
            }
        }

        // POST /api/admin/exchange
        public async Task ExchangeTokenAsync(string loginToken, string deviceId = "GMTool_Desktop")
        {
            try
            {
                var request = new ExchangeRequest(LoginToken: loginToken, DeviceId: deviceId);

                var response = await _httpClient.PostAsJsonAsync("/api/admin/exchange", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"토큰 교환 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"토큰 교환 실패: {response.StatusCode}");
                }
                var result = await response.Content.ReadFromJsonAsync<ExchangeResponse>();
                if (result == null || string.IsNullOrEmpty(result.AccessToken) || string.IsNullOrEmpty(result.RefreshToken))
                {
                    throw new InvalidOperationException("토큰 교환 응답이 유효하지 않습니다.");
                }

                // 토큰 매니저에 액세스 토큰 및 리프레시 토큰 설정
                _tokenManager.SetTokens(result.AccessToken, result.RefreshToken);
                _logService.Success("토큰 교환 성공");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "토큰 교환 중 예외 발생");
                throw;
            }
        }

        public async Task<bool> RefreshTokenAsync(string deviceId = "GMTool_Desktop")
        {
            // POST /api/admin/refresh
            try
            {
                var request = new RefreshRequest
                (
                    RefreshToken: _tokenManager.RefreshToken ?? throw new InvalidOperationException("리프레시 토큰이 없습니다."),
                    DeviceId: deviceId
                );

                var response = await _httpClient.PostAsJsonAsync("/api/admin/refresh", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"토큰 갱신 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"토큰 갱신 실패: {response.StatusCode}");
                }
                var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
                if (result == null || string.IsNullOrEmpty(result.Message) || string.IsNullOrEmpty(result.AccToken))
                {
                    throw new InvalidOperationException("토큰 갱신 응답이 유효하지 않습니다.");
                }

                // 토큰 매니저에 액세스 토큰 갱신
                _tokenManager.UpdateAccessToken(result.AccToken);
                _logService.Success("토큰 갱신 성공");

                return true;
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "토큰 갱신 중 예외 발생");
                throw;
            }
        }

        public async Task LogoutAsync(string deviceId = "GMTool_Desktop")
        {
            // POST /api/auth/logout

            try
            {
                var request = new LogoutRequest(DeviceId: deviceId);

                var response = await _httpClient.PostAsJsonAsync("/api/admin/logout", request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"로그아웃 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"로그아웃 실패: {response.StatusCode}");
                }
                _logService.Success("로그아웃 성공");

            }
            catch (Exception ex)
            {
                _logService.Error(ex, "로그아웃 중 예외 발생");
                throw;
            }
        }
    }
}
