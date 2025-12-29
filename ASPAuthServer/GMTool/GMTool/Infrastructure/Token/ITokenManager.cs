namespace GMTool.Infrastructure.Token
{
    public interface ITokenManager
    {
        /// <summary>
        /// Access Token
        /// </summary>
        string? AccessToken { get; }

        /// <summary>
        /// Refresh Token
        /// </summary>
        string? RefreshToken { get; }

        /// <summary>
        /// 토큰 설정
        /// </summary>
        void SetTokens(string accessToken, string refreshToken);

        /// <summary>
        /// Access Token만 업데이트 (Refresh 시)
        /// </summary>
        void UpdateAccessToken(string accessToken);

        /// <summary>
        /// 토큰 클리어
        /// </summary>
        void ClearTokens();

        /// <summary>
        /// 유효한 토큰이 있는지 확인
        /// </summary>
        bool HasValidTokens();
    }
}
