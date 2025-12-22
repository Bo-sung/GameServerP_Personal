using System.ComponentModel.DataAnnotations;

namespace AuthServer.Settings
{
    public class RateLimitSettings
    {
        public bool EnableRateLimit { get; set; } = true;

        [Range(1, 1000)]
        public int RequestsPerMinute { get; set; } = 60;

        [Range(1, 10000)]
        public int LoginRequestsPerHour { get; set; } = 10;

        public bool EnableIpBlocking { get; set; } = true;

        [Range(1, 1440)]
        public int BlockDurationMinutes { get; set; } = 60;
    }
}
