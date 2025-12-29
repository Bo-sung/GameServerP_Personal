using AuthServer.Models;
using AuthServer.Services.Tokens;

namespace AuthServer.Services.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// 로그인 처리
        /// </summary>
        /// <param name="username">사용자명</param>
        /// <param name="password">비밀번호</param>
        /// <param name="deviceId">디바이스 ID</param>
        /// <param name="audience">대상 (GameClient 또는 AdminPanel)</param>
        Task<(bool Success, string? Token, string? Message, User? User)> LoginAsync(string username, string password, string deviceId, string audience = "GameClient");
        Task<(bool Success, int? UserId, string? Message)> RegisterAsync(string username, string email, string password);
        Task<bool> LogoutAsync(int userId);
    }

    public interface IAdminAuthService
    {
        Task<(bool Success, string? Message)> LockUserAsync(int userId, bool lockUser, int? durationMinutes = null);
        Task<(bool Success, string? Message)> ResetPasswordAsync(int userId, string newPassword);
    }
}
