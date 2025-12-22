using MySql.Data.MySqlClient;

namespace AuthServer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int LoginAttempts { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
    }
}
