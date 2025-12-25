using System.ComponentModel.DataAnnotations;

namespace AuthServer.Settings
{
    public class JwtSettings
    {
        [Required]
        [MinLength(32, ErrorMessage = "SecretKey must be at least 32 characters")]
        public string SecretKey { get; set; } = string.Empty;

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 60, ErrorMessage = "LoginTokenExpirationMinutes must be between 1 and 60")]
        public int LoginTokenExpirationMinutes { get; set; }

        [Range(1, 1440, ErrorMessage = "AccessTokenExpirationMinutes must be between 1 and 1440")]
        public int AccessTokenExpirationMinutes { get; set; }

        [Range(1, 365, ErrorMessage = "RefreshTokenExpirationDays must be between 1 and 365")]
        public int RefreshTokenExpirationDays { get; set; }

        [Range(1, 168, ErrorMessage = "UsedLoginTokenRetentionHours must be between 1 and 168 (7 days)")]
        public int UsedLoginTokenRetentionHours { get; set; } = 24;
    }
}
