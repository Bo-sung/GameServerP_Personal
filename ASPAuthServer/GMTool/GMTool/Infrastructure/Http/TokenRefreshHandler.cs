using GMTool.Infrastructure.Config;
using GMTool.Infrastructure.Token;
using GMTool.Services.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GMTool.Infrastructure.Http
{
    public class TokenRefreshHandler : DelegatingHandler
    {
        private readonly ITokenManager _tokenManager;
        private readonly AppSettings _appSettings;
        private readonly ILogService _logService;
        private readonly HttpClient _refreshClient;

        public TokenRefreshHandler(
            ITokenManager tokenManager,
            AppSettings appSettings,
            ILogService logService)
        {
            _tokenManager = tokenManager;
            _appSettings = appSettings;
            _logService = logService;

            // Refresh 전용 HttpClient (순환 참조 방지)
            _refreshClient = new HttpClient
            {
                BaseAddress = new Uri(appSettings.ApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(appSettings.RequestTimeoutSeconds)
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // 1. Access Token 헤더 추가
            if (!string.IsNullOrEmpty(_tokenManager.AccessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);
            }

            // 2. 요청 전송
            var response = await base.SendAsync(request, cancellationToken);

            // 3. 401 Unauthorized 시 Refresh Token으로 재시도
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logService.Warning("401 Unauthorized 발생, 토큰 갱신 시도 중...");

                try
                {
                    // Refresh Token으로 갱신 시도
                    var refreshSuccess = await RefreshTokenInternalAsync();

                    if (refreshSuccess)
                    {
                        _logService.Success("토큰 갱신 성공, 요청 재시도");

                        // 새로운 Access Token으로 재시도
                        request.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);

                        response = await base.SendAsync(request, cancellationToken);
                    }
                    else
                    {
                        // 4. Refresh Token 만료 시 로그아웃
                        _logService.Error("Refresh Token 만료, 재로그인 필요");
                        _tokenManager.ClearTokens();
                        // TODO: LoginWindow로 네비게이션 (ViewModel에서 처리)
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error(ex, "토큰 갱신 중 예외 발생, 토큰 클리어");
                    _tokenManager.ClearTokens();
                    // TODO: LoginWindow로 네비게이션 (ViewModel에서 처리)
                }
            }

            return response;
        }

        private async Task<bool> RefreshTokenInternalAsync()
        {
            if (string.IsNullOrEmpty(_tokenManager.RefreshToken))
            {
                return false;
            }

            try
            {
                var request = new
                {
                    refreshToken = _tokenManager.RefreshToken,
                    deviceId = _appSettings.DeviceId
                };

                var response = await _refreshClient.PostAsJsonAsync("/api/admin/refresh", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();

                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        _tokenManager.SetTokens(result.AccessToken, result.RefreshToken);
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private class RefreshTokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
        }
    }
}
