using AuthServer.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
    public class RefreshToken : IToken
    {
        public const string REDIS_KEY_FORMAT = "refresh_tokens:{0}:{1}";
        public string Jti { get; private set; }
        public int UserId { get; private set; }
        public string DeviceId { get; private set; }
        public DateTime IssuedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public string JwtString { get; private set; }

        // private 생성자 - 외부에서 직접 생성 불가
        private RefreshToken() { }

        /// <summary>
        /// 리프레시 토큰 생성 (Factory Method)
        /// </summary>
        public static RefreshToken Create(int userId, string deviceId, JwtSettings settings)
        {
            var token = new RefreshToken
            {
                Jti = Guid.NewGuid().ToString(),
                UserId = userId,
                DeviceId = deviceId,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(settings.RefreshTokenExpirationDays)
            };

            // 리프레시 토큰만의 고유한 Claims 정의
            token.JwtString = token.GenerateJwt(settings);
            return token;
        }

        private string GenerateJwt(JwtSettings settings)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Jti),
                new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()),
                new Claim("userId", UserId.ToString()),
                new Claim("deviceId", DeviceId),
                new Claim("type", "refresh"),
                new Claim("purpose", "token_refresh")  // 리프레시 토큰 전용
            };

            return JwtHelper.GenerateToken(claims, settings, ExpiresAt);
        }

        public string GetRedisKey()
        {
            return BuildRedisKey(UserId, DeviceId);
        }

        public static string BuildRedisKey(int userId, string deviceId)
        {
            return string.Format(REDIS_KEY_FORMAT, userId, deviceId);
        }

        public string GetRedisValue()
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                jti = Jti,
                userId = UserId,
                deviceId = DeviceId,
                issuedAt = IssuedAt,
                expiresAt = ExpiresAt
            });
        }

        public long GetTTL()
        {
            var ttl = (long)(ExpiresAt - DateTime.UtcNow).TotalSeconds;
            return ttl > 0 ? ttl : 0;
        }

        public ITokenService.TokenType GetTokenType()
        {
            return ITokenService.TokenType.Refresh;
        }

        public string GetTokenString()
        {
            return JwtString;
        }
    }
}
