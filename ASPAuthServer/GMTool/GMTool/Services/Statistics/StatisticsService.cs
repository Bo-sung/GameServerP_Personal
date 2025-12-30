using GMTool.Infrastructure.Config;
using GMTool.Services.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace GMTool.Services.Statistics
{
    /// <param name="TotalUsers"> 총 사용자 수 </param>
    /// <param name="ActiveUsers"> 활성 사용자 수 </param>
    /// <param name="LockedUsers"> 잠긴 계정 수 </param>
    /// <param name="OnlineUsers"> 현재 온라인 사용자 수 </param>
    /// <param name="TodayRegistrations"> 오늘 가입한 사용자 수 </param>
    /// <param name="TodayLogins"> 오늘 로그인 수 </param>
    public record ServerStatistics(int TotalUsers, int ActiveUsers, int LockedUsers, int OnlineUsers, int TodayRegistrations, int TodayLogins);

    public class StatisticsService : IStatisticsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogService _logService;
        private readonly AppSettings _appSettings;

        // 캐싱을 위한 필드
        private ServerStatistics? _cachedStatistics;
        private DateTime _lastRefresh;
        private const int CACHE_DURATION_SECONDS = 30;

        public StatisticsService(
            HttpClient httpClient,
            ILogService logService,
            AppSettings appSettings)
        {
            _httpClient = httpClient;
            _logService = logService;
            _appSettings = appSettings;
        }

        public async Task<ServerStatistics> GetStatisticsAsync(bool forceRefresh = false)
        {
            // GET /api/admin/statistics
            try
            {
                // 캐시 확인 (30초 이내면 캐시 사용)
                if (!forceRefresh &&
                    _cachedStatistics != null &&
                    (DateTime.Now - _lastRefresh).TotalSeconds < CACHE_DURATION_SECONDS)
                {
                    _logService.Debug("통계 데이터 캐시 사용");
                    return _cachedStatistics;
                }

                _logService.Info("서버 통계 조회 중...");

                var url = "/api/admin/statistics";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"서버 통계 조회 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"서버 통계 조회 실패: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<ServerStatistics>();

                if (result == null)
                    throw new InvalidOperationException("서버 통계 응답이 유효하지 않습니다.");

                // 캐시 업데이트
                _cachedStatistics = result;
                _lastRefresh = DateTime.Now;

                _logService.Success($"서버 통계 로드 완료: 총 {result.TotalUsers}명, 온라인 {result.OnlineUsers}명");

                return result;
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "서버 통계 조회 중 예외 발생");
                throw;
            }
        }
    }
}
