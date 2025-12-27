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
        Task<string?> CreateToken(ITokenService.TokenType type, int user_id, string deviceId);

        Task<bool> ValidateTokenAsync(string token, string deviceId);

        Task<bool> ValidateTokenAsync(string token, string deviceId, TokenType type);

        Task<bool> RevokeTokenAsync(string token, string deviceId, TokenType type);

        Task<string?> RefreshRT(string token);

        Task<(string? AccessToken, string? RefreshToken)> ExchangeTokensAsync(string loginToken);

        Task<bool> MarkLoginTokenAsUsedAsync(string token);

        Task<bool> RevokeAllUserTokensAsync(int userId, string deviceId);
    }
}
