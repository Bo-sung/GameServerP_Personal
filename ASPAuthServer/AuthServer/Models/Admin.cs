namespace AuthServer.Models
{
    /// <summary>
    /// 관리자 계정 모델 (User와 별도 테이블)
    /// </summary>
    public class Admin
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

        // 관리자 전용 필드
        public string Role { get; set; } = "Admin"; // "SuperAdmin", "Admin", "Moderator" 등
        public string? Permissions { get; set; } // JSON 형태의 권한 목록 (예: ["users.read", "users.write", "statistics.read"])
    }
}
