using AuthServer.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
    public class LogintToken : IToken
    {
        public const string ACTIVE_REDIS_KEY_FORMAT = "active_login_tokens:{0}";
        public const string USED_REDIS_KEY_FORMAT = "used_login_tokens:{0}";

        private static JwtSettings _JwtSettings = null!;
        public string Jti { get; private set; }
        public int UserId { get; private set; }
        public string DeviceId { get; private set; }
        public DateTime IssuedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public string JwtString { get; private set; }

        // private 생성자 - 외부에서 직접 생성 불가
        private LogintToken() { }

        /// <summary>
        /// 로그인 토큰 생성 (Factory Method)
        /// </summary>
        public static LogintToken Create(int userId, string deviceId, JwtSettings settings)
        {
            if (_JwtSettings == null)
            {
                _JwtSettings = settings;
            }
            var token = new LogintToken
            {
                Jti = Guid.NewGuid().ToString(),
                UserId = userId,
                DeviceId = deviceId,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_JwtSettings.LoginTokenExpirationMinutes)
            };

            // 로그인 토큰만의 고유한 Claims 정의
            token.JwtString = token.GenerateJwt(_JwtSettings);
            return token;
        }

        private string GenerateJwt(JwtSettings settings)
        {
            if (_JwtSettings == null)
            {
                _JwtSettings = settings;
            }
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Jti),
                new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()),
                new Claim("userId", UserId.ToString()),
                new Claim("deviceId", DeviceId),
                new Claim("type", "login"),
                new Claim("purpose", "token_exchange"),  // 로그인 토큰 전용
                new Claim("oneTimeUse", "true")          // 1회용 토큰 표시
            };

            return JwtHelper.GenerateToken(claims, _JwtSettings, ExpiresAt);
        }

        public string GetRedisKey()
        {
            return BuildActiveRedisKey(Jti);
        }

        public static string BuildActiveRedisKey(string jti)
        {
            return string.Format(ACTIVE_REDIS_KEY_FORMAT, jti);
        }

        public static string BuildUsedRedisKey(string jti)
        {
            return string.Format(USED_REDIS_KEY_FORMAT, jti);
        }

        public string GetRedisValue()
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = UserId,
                deviceId = DeviceId,
                issuedAt = IssuedAt,
                expiresAt = ExpiresAt
            });
        }

        public string GetTokenString()
        {
            return JwtString;
        }

        public ITokenService.TokenType GetTokenType()
        {
            return ITokenService.TokenType.Login;
        }

        public long GetTTL()
        {
            var ttl = (long)(ExpiresAt - DateTime.UtcNow).TotalSeconds;
            return ttl > 0 ? ttl : 0;
        }
    }
}
