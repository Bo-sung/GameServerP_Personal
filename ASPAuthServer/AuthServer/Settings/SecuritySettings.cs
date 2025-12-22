using System.ComponentModel.DataAnnotations;

namespace AuthServer.Settings
{
    public class SecuritySettings
    {
        [Range(1, 100)]
        public int MaxLoginAttempts { get; set; } = 5;

        [Range(1, 1440)]
        public int LockoutDurationMinutes { get; set; } = 5;

        [Range(1000, 100000)]
        public int PasswordHashIterations { get; set; } = 10000;

        public bool EnableIpWhitelist { get; set; } = false;

        public List<string> WhitelistedIps { get; set; } = new List<string>();
    }
}
