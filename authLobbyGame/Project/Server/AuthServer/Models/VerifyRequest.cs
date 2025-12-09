namespace AuthServer.Models
{
    /// <summary>
    /// 토큰 검증 요청 모델
    /// Access Token의 유효성을 확인하는 요청
    /// </summary>
    public class VerifyRequest
    {
        /// <summary>
        /// 검증할 JWT Access Token
        /// </summary>
        public string Token { get; set; }
    }
}
