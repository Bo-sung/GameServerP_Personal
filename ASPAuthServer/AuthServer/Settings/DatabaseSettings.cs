using System.ComponentModel.DataAnnotations;

namespace AuthServer.Settings
{
    public class DatabaseSettings
    {
        [Required]
        public string MySQLConnection { get; set; } = string.Empty;

        [Required]
        public string RedisConnection { get; set; } = string.Empty;

        [Range(1, 100)]
        public int MySQLConnectionPoolSize { get; set; } = 10;

        [Range(1, 100)]
        public int RedisConnectionPoolSize { get; set; } = 10;

        public bool EnableConnectionRetry { get; set; } = true;

        [Range(1, 10)]
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
