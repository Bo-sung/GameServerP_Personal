using AuthServer.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
    public class AccessToken : IToken
    {
        public string Jti { get; private set; }
        public int UserId { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string JwtString { get; private set; }

        // private 생성자 - 외부에서 직접 생성 불가
        private AccessToken() { }

        /// <summary>
        /// 액세스 토큰 생성 (Factory Method)
        /// </summary>
        public static AccessToken Create(int userId, JwtSettings settings)
        {
            var token = new AccessToken
            {
                Jti = Guid.NewGuid().ToString(),
                UserId = userId,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(settings.AccessTokenExpirationMinutes)
            };

            // 액세스 토큰만의 고유한 Claims 정의
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
                new Claim("type", "access"),
                new Claim("purpose", "api_access")  // 액세스 토큰 전용
            };

            return JwtHelper.GenerateToken(claims, settings, ExpiresAt);
        }

        public string GetRedisKey()
        {
            // 요청하면 에러임. 나와선 안됨.
            // AccessToken은 Redis에 저장하지 않음.
            throw new NotImplementedException();
        }

        public string GetRedisValue()
        {
            // 요청하면 에러임. 나와선 안됨.
            // AccessToken은 Redis에 저장하지 않음.
            throw new NotImplementedException();
        }

        public long GetTTL()
        {
            // ttl 계산 (초 단위. 현재 시간과 만료 시간의 차이)
            var ttl = (long)(ExpiresAt - DateTime.UtcNow).TotalSeconds;
            return ttl > 0 ? ttl : 0;
        }

        public ITokenService.TokenType GetTokenType()
        {
            return ITokenService.TokenType.Access;
        }

        public string GetTokenString()
        {
            return JwtString;
        }
    }
}
