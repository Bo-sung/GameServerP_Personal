using AuthServer.Models;
using AuthServer.Services.Tokens;

namespace AuthServer.Services.Auth
{
    public interface IAuthService
    {
        Task<(bool Success, string? Token, string? Message, User? User)> LoginAsync(string username, string password, string deviceId);
        Task<(bool Success, int? UserId, string? Message)> RegisterAsync(string username, string email, string password);
        Task<bool> LogoutAsync(int userId);
    }
}
