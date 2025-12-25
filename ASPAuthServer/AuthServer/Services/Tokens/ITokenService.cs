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

        Task<bool> ValidateTokenAsync(string token, TokenType type);

        Task<bool> RevokeTokenAsync(string token, TokenType type);

        Task<bool> MarkLoginTokenAsUsedAsync(string token);
    }
}
