using System.Threading.Tasks;

namespace GMTool.Services.Statistics
{
    public interface IStatisticsService
    {
        /// <summary>
        /// 서버 통계 조회
        /// </summary>
        Task<ServerStatistics> GetStatisticsAsync(bool forceRefresh = false);
    }
}
