using System;

namespace GMTool.Models
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int LoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }

        public string StatusDisplay => IsActive ? "✅ 활성" : "❌ 비활성";
        public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm");
        public string LastLoginAtDisplay => LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "없음";
        public bool IsLocked => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    }
}
