using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LobbyServer.Services
{
    /// <summary>
    /// Auth 서버와 통신하기 위한 HTTP 클라이언트 서비스
    /// JWT 토큰 검증 요청을 처리
    /// </summary>
    public class AuthServerClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _verifyEndpoint;
        private readonly int _timeoutSeconds;

        /// <summary>
        /// Auth 서버 클라이언트 생성자
        /// </summary>
        /// <param name="config">설정 파일</param>
        public AuthServerClient(IConfiguration config)
        {
            _baseUrl = config.GetValue<string>("AuthServer:BaseUrl") ?? "http://localhost:5000";
            _verifyEndpoint = config.GetValue<string>("AuthServer:VerifyEndpoint") ?? "/api/auth/verify";
            _timeoutSeconds = config.GetValue<int>("AuthServer:TimeoutSeconds");
            if (_timeoutSeconds <= 0) _timeoutSeconds = 5;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(_timeoutSeconds)
            };
        }

        /// <summary>
        /// JWT 토큰 검증 요청
        /// Auth 서버의 /api/auth/verify 엔드포인트 호출
        /// </summary>
        /// <param name="token">검증할 JWT Access Token</param>
        /// <returns>검증 결과 (성공 시 사용자 ID 포함)</returns>
        public async Task<TokenVerifyResult> VerifyTokenAsync(string token)
        {
            try
            {
                // 요청 데이터 생성
                var requestData = new { Token = token };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // HTTP POST 요청
                var response = await _httpClient.PostAsync(_verifyEndpoint, content);

                // 응답 읽기
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // 성공 응답 파싱
                    var result = JsonSerializer.Deserialize<TokenVerifyResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null && result.Valid)
                    {
                        return new TokenVerifyResult
                        {
                            IsValid = true,
                            UserId = result.UserId,
                            Message = "Token is valid"
                        };
                    }
                }

                // 실패 응답
                return new TokenVerifyResult
                {
                    IsValid = false,
                    Message = $"Token verification failed: {response.StatusCode}"
                };
            }
            catch (HttpRequestException ex)
            {
                return new TokenVerifyResult
                {
                    IsValid = false,
                    Message = $"Auth server connection failed: {ex.Message}"
                };
            }
            catch (TaskCanceledException)
            {
                return new TokenVerifyResult
                {
                    IsValid = false,
                    Message = "Auth server request timeout"
                };
            }
            catch (Exception ex)
            {
                return new TokenVerifyResult
                {
                    IsValid = false,
                    Message = $"Token verification error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Auth 서버 연결 상태 확인
        /// </summary>
        /// <returns>연결 가능 여부</returns>
        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/");
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        /// <summary>
        /// Auth 서버 응답 모델
        /// </summary>
        private class TokenVerifyResponse
        {
            public bool Valid { get; set; }
            public string? UserId { get; set; }
        }
    }

    /// <summary>
    /// 토큰 검증 결과
    /// </summary>
    public class TokenVerifyResult
    {
        /// <summary>
        /// 토큰 유효 여부
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 사용자 ID (검증 성공 시)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 메시지 (에러 또는 성공 메시지)
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
