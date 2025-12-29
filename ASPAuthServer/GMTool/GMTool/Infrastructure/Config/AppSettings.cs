namespace GMTool.Infrastructure.Config
{
    public class AppSettings
    {
        /// <summary>
        /// API Base URL
        /// </summary>
        public string ApiBaseUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// Device ID (고정값)
        /// </summary>
        public string DeviceId { get; set; } = "GMTool_Desktop";

        /// <summary>
        /// 최대 로그 개수
        /// </summary>
        public int MaxLogCount { get; set; } = 500;

        /// <summary>
        /// HTTP 요청 타임아웃 (초)
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;
    }
}
