using AuthServer.Models;

namespace AuthServer.Data.Repositories
{
    public interface IAdminRepository
    {
        Task<Admin?> GetByIdAsync(int id);
        Task<Admin?> GetByUsernameAsync(string username);
        Task<Admin?> GetByEmailAsync(string email);
        Task<int> CreateAsync(Admin admin);
        Task<bool> UpdateAsync(Admin admin);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(string username, string email);
    }
}
