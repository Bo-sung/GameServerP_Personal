namespace AuthServer.Models
{
    /// <summary>
    /// 토큰 갱신 요청 모델
    /// Refresh Token을 이용해 새로운 Access Token 발급 요청
    /// </summary>
    public class RefreshRequest
    {
        /// <summary>
        /// Refresh Token
        /// 로그인 시 발급된 토큰을 사용해 Access Token 재발급
        /// </summary>
        public string RefreshToken { get; set; }
    }
}
