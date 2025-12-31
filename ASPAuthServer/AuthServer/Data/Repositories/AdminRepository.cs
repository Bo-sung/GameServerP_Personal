using AuthServer.Models;

namespace AuthServer.Data.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly MySQLWrapper<Admin> _db;

        public AdminRepository(IDbConnectionFactory connectionFactory)
        {
            _db = new MySQLWrapper<Admin>(connectionFactory);
        }

        public async Task<Admin?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Admins WHERE Id = @Id AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Id", id }
            });
        }

        public async Task<Admin?> GetByUsernameAsync(string username)
        {
            var sql = "SELECT * FROM Admins WHERE Username = @Username AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Username", username }
            });
        }

        public async Task<Admin?> GetByEmailAsync(string email)
        {
            var sql = "SELECT * FROM Admins WHERE Email = @Email AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Email", email }
            });
        }

        public async Task<int> CreateAsync(Admin admin)
        {
            var sql = @"INSERT INTO Admins (Username, Email, PasswordHash, CreatedAt, IsActive, LoginAttempts, Role, Permissions)
                       VALUES (@Username, @Email, @PasswordHash, @CreatedAt, @IsActive, @LoginAttempts, @Role, @Permissions);
                       SELECT LAST_INSERT_ID();";

            return await _db.ExecuteScalarAsync<int>(sql, new Dictionary<string, object>
            {
                { "Username", admin.Username },
                { "Email", admin.Email },
                { "PasswordHash", admin.PasswordHash },
                { "CreatedAt", admin.CreatedAt },
                { "IsActive", admin.IsActive },
                { "LoginAttempts", admin.LoginAttempts },
                { "Role", admin.Role },
                { "Permissions", admin.Permissions ?? (object)DBNull.Value }
            });
        }

        public async Task<bool> UpdateAsync(Admin admin)
        {
            var sql = @"UPDATE Admins
                       SET Username = @Username,
                           Email = @Email,
                           LastLoginAt = @LastLoginAt,
                           LoginAttempts = @LoginAttempts,
                           LockedUntil = @LockedUntil,
                           Role = @Role,
                           Permissions = @Permissions
                       WHERE Id = @Id";

            var affected = await _db.ExecuteAsync(sql, new Dictionary<string, object>
            {
                { "Id", admin.Id },
                { "Username", admin.Username },
                { "Email", admin.Email },
                { "LastLoginAt", admin.LastLoginAt ?? (object)DBNull.Value },
                { "LoginAttempts", admin.LoginAttempts },
                { "LockedUntil", admin.LockedUntil ?? (object)DBNull.Value },
                { "Role", admin.Role },
                { "Permissions", admin.Permissions ?? (object)DBNull.Value }
            });

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sql = "UPDATE Admins SET IsActive = 0 WHERE Id = @Id";
            var affected = await _db.ExecuteAsync(sql, new Dictionary<string, object>
            {
                { "Id", id }
            });

            return affected > 0;
        }

        public async Task<bool> ExistsAsync(string username, string email)
        {
            var sql = @"SELECT COUNT(*) FROM Admins
                       WHERE (Username = @Username OR Email = @Email) AND IsActive = 1";

            var count = await _db.ExecuteScalarAsync<int>(sql, new Dictionary<string, object>
            {
                { "Username", username },
                { "Email", email }
            });

            return count > 0;
        }
    }
}
