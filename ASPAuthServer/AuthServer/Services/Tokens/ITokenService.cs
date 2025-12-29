namespace AuthServer.Services.Tokens
{
    public interface ITokenService
    {
        public enum TokenType
        {
            Login,
            Access,
            Refresh
        }

        /// <summary>
        /// 토큰 생성
        /// </summary>
        /// <param name="type">토큰 타입</param>
        /// <param name="user_id">사용자 ID</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <param name="audience">대상 (GameClient 또는 AdminPanel)</param>
        Task<string?> CreateToken(ITokenService.TokenType type, int user_id, string deviceId, string audience = "GameClient");

        Task<bool> ValidateTokenAsync(string token, string deviceId);

        Task<bool> ValidateTokenAsync(string token, string deviceId, TokenType type);

        Task<bool> RevokeTokenAsync(string token, string deviceId, TokenType type);

        /// <summary>
        /// Access Token 갱신
        /// </summary>
        /// <param name="token">Refresh Token</param>
        /// <param name="audience">대상 (GameClient 또는 AdminPanel)</param>
        Task<string?> RefreshAccessTokenAsync(string token, string audience = "GameClient");

        /// <summary>
        /// Login Token을 Access/Refresh Token으로 교환
        /// </summary>
        /// <param name="loginToken">Login Token</param>
        /// <param name="audience">대상 (GameClient 또는 AdminPanel)</param>
        Task<(string? AccessToken, string? RefreshToken)> ExchangeTokensAsync(string loginToken, string audience = "GameClient");

        Task<bool> MarkLoginTokenAsUsedAsync(string token);

        Task<bool> RevokeAllUserTokensAsync(int userId, string deviceId);

        /// <summary>
        /// 토큰에서 UserId 추출 (검증 포함)
        /// </summary>
        Task<(bool Success, int UserId, string? ErrorMessage)> ExtractUserIdFromTokenAsync(string token, TokenType expectedType);
    }
}
