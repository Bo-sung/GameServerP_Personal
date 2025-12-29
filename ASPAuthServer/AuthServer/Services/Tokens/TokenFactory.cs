using AuthServer.Settings;
using Microsoft.Extensions.Options;

namespace AuthServer.Services.Tokens
{
    /// <summary>
    /// 토큰 생성을 위한 팩토리 클래스 (audience에 따라 다른 JwtSettings 사용)
    /// </summary>
    public class TokenFactory
    {
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettingsSnapshot;

        public TokenFactory(IOptionsSnapshot<JwtSettings> jwtSettingsSnapshot)
        {
            _jwtSettingsSnapshot = jwtSettingsSnapshot;
        }

        /// <summary>
        /// audience에 따라 올바른 JwtSettings 반환
        /// </summary>
        private JwtSettings GetJwtSettings(string audience)
        {
            return audience switch
            {
                "GameClient" => _jwtSettingsSnapshot.Get("Game"),
                "AdminPanel" => _jwtSettingsSnapshot.Get("Admin"),
                _ => throw new ArgumentException($"Unknown audience: {audience}", nameof(audience))
            };
        }

        /// <summary>
        /// 로그인 토큰 생성 (1분 유효, 1회용)
        /// </summary>
        public IToken CreateLoginToken(int userId, string deviceId, string audience)
        {
            var jwtSettings = GetJwtSettings(audience);
            return LogintToken.Create(userId, deviceId, jwtSettings);
        }

        public IToken CreateAccessToken(int userId, string audience)
        {
            var jwtSettings = GetJwtSettings(audience);
            return AccessToken.Create(userId, jwtSettings);
        }

        public IToken CreateRefreshToken(int userId, string deviceId, string audience)
        {
            var jwtSettings = GetJwtSettings(audience);
            return RefreshToken.Create(userId, deviceId, jwtSettings);
        }
    }
}
