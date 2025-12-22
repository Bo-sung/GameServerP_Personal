using System.ComponentModel.DataAnnotations;

namespace AuthServer.Settings
{
    public class SessionSettings
    {
        public bool AllowMultipleLogin { get; set; } = false;

        [Range(1, 10080)]
        public int SessionTimeoutMinutes { get; set; } = 30;

        public bool EnableDeviceTracking { get; set; } = false;

        public int MaxDevicesPerUser { get; set; } = 3;
    }
}
