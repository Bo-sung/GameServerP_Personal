using AuthServer.Models;

namespace AuthServer.Services
{
    public interface IAuthService
    {
        Task<(bool Success, string? Token, string? Message, User? User)> LoginAsync(string username, string password);
        Task<(bool Success, int? UserId, string? Message)> RegisterAsync(string username, string email, string password);
        Task<bool> LogoutAsync(int userId);
        Task<User?> GetUserByTokenAsync(string token);
        Task<bool> ValidateTokenAsync(string token);
    }
}
