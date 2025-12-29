namespace GMTool.Infrastructure.Token
{
    public class TokenManager : ITokenManager
    {
        private string? _accessToken;
        private string? _refreshToken;

        public string? AccessToken => _accessToken;
        public string? RefreshToken => _refreshToken;

        public void SetTokens(string accessToken, string refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
        }

        public void UpdateAccessToken(string accessToken)
        {
            _accessToken = accessToken;
        }

        public void ClearTokens()
        {
            _accessToken = null;
            _refreshToken = null;
        }

        public bool HasValidTokens()
        {
            return !string.IsNullOrEmpty(_accessToken) &&
                   !string.IsNullOrEmpty(_refreshToken);
        }
    }
}
