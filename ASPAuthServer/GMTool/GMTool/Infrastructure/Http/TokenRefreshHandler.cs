using GMTool.Infrastructure.Token;
using GMTool.Services.Auth;
using GMTool.Services.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GMTool.Infrastructure.Http
{
    public class TokenRefreshHandler : DelegatingHandler
    {
        private readonly ITokenManager _tokenManager;
        private readonly IAuthService _authService;
        private readonly ILogService _logService;

        public TokenRefreshHandler(
            ITokenManager tokenManager,
            IAuthService authService,
            ILogService logService)
        {
            _tokenManager = tokenManager;
            _authService = authService;
            _logService = logService;
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
                    var refreshSuccess = await _authService.RefreshTokenAsync();

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
    }
}
