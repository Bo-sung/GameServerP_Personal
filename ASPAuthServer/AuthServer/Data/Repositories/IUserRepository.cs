using AuthServer.Models;

namespace AuthServer.Data.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailAsync(string email);
        Task<int> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(string username, string email);
        Task<List<User>> GetAllActiveUsersAsync();
        Task<int> GetCountAsync();
    }
}
