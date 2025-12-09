namespace AuthServer.Models
{
    /// <summary>
    /// 사용자 로그인 요청 모델
    /// 클라이언트가 로그인 API 호출 시 전송하는 데이터
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 사용자명
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 비밀번호 (평문)
        /// </summary>
        public string Password { get; set; }
    }
}
