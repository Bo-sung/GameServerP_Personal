namespace AuthServer.Services.Tokens
{
    public interface IToken
    {
        /// <summary>
        /// Redis 저장용 Key 생성
        /// 예: "used_login_tokens:lt-abc123"
        /// </summary>
        string GetRedisKey();

        /// <summary>
        /// Redis에 저장할 Value (JSON 직렬화 가능)
        /// </summary>
        string GetRedisValue();

        /// <summary>
        /// TTL (Time To Live) - 초 단위
        /// </summary>
        long GetTTL();

        /// <summary>
        /// 토큰 타입 (login, access, refresh)
        /// </summary>
        ITokenService.TokenType GetTokenType();

        /// <summary>
        /// JWT 토큰 문자열 (실제 클라이언트에게 전달하는 값)
        /// </summary>
        string GetTokenString();
    }
}
