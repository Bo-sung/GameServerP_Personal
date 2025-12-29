using System.Threading.Tasks;

namespace GMTool.Services.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// 관리자 로그인 (Login Token 획득)
        /// </summary>
        Task<string> LoginAsync(string username, string password, string deviceId = "GMTool_Desktop");

        /// <summary>
        /// Login Token을 Access Token + Refresh Token으로 교환
        /// </summary>
        Task ExchangeTokenAsync(string loginToken, string deviceId = "GMTool_Desktop");

        /// <summary>
        /// Refresh Token으로 Access Token 갱신
        /// </summary>
        Task<bool> RefreshTokenAsync(string deviceId = "GMTool_Desktop");

        /// <summary>
        /// 로그아웃
        /// </summary>
        Task LogoutAsync(string deviceId = "GMTool_Desktop");
    }
}
