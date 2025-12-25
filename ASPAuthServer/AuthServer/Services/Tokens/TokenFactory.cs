using AuthServer.Settings;
using Microsoft.Extensions.Options;

namespace AuthServer.Services.Tokens
{
    /// <summary>
    /// 토큰 생성을 위한 팩토리 클래스 (DI를 통한 JwtSettings 주입)
    /// </summary>
    public class TokenFactory
    {
        private readonly JwtSettings _jwtSettings;

        public TokenFactory(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        /// <summary>
        /// 로그인 토큰 생성 (1분 유효, 1회용)
        /// </summary>
        public IToken CreateLoginToken(int userId, string deviceId)
        {
            return LogintToken.Create(userId, deviceId, _jwtSettings);
        }

        public IToken CreateAccessToken(int userId)
        {
            return AccessToken.Create(userId, _jwtSettings);
        }

        public IToken CreateRefreshToken(int userId, string deviceId)
        {
            return RefreshToken.Create(userId, deviceId, _jwtSettings);
        }
    }
}
