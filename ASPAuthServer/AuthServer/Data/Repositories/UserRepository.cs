using AuthServer.Models;

namespace AuthServer.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly MySQLWrapper<User> _db;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _db = new MySQLWrapper<User>(connectionFactory);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Users WHERE Id = @Id AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Id", id }
            });
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var sql = "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Username", username }
            });
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var sql = "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1";
            return await _db.QueryFirstOrDefaultAsync(sql, new Dictionary<string, object>
            {
                { "Email", email }
            });
        }

        public async Task<int> CreateAsync(User user)
        {
            var sql = @"INSERT INTO Users (Username, Email, PasswordHash, CreatedAt, IsActive, LoginAttempts)
                       VALUES (@Username, @Email, @PasswordHash, @CreatedAt, @IsActive, @LoginAttempts);
                       SELECT LAST_INSERT_ID();";

            return await _db.ExecuteScalarAsync<int>(sql, new Dictionary<string, object>
            {
                { "Username", user.Username },
                { "Email", user.Email },
                { "PasswordHash", user.PasswordHash },
                { "CreatedAt", user.CreatedAt },
                { "IsActive", user.IsActive },
                { "LoginAttempts", user.LoginAttempts }
            });
        }

        public async Task<bool> UpdateAsync(User user)
        {
            var sql = @"UPDATE Users
                       SET Username = @Username,
                           Email = @Email,
                           LastLoginAt = @LastLoginAt,
                           LoginAttempts = @LoginAttempts,
                           LockedUntil = @LockedUntil
                       WHERE Id = @Id";

            var affected = await _db.ExecuteAsync(sql, new Dictionary<string, object>
            {
                { "Id", user.Id },
                { "Username", user.Username },
                { "Email", user.Email },
                { "LastLoginAt", user.LastLoginAt ?? (object)DBNull.Value },
                { "LoginAttempts", user.LoginAttempts },
                { "LockedUntil", user.LockedUntil ?? (object)DBNull.Value }
            });

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sql = "UPDATE Users SET IsActive = 0 WHERE Id = @Id";
            var affected = await _db.ExecuteAsync(sql, new Dictionary<string, object>
            {
                { "Id", id }
            });

            return affected > 0;
        }

        public async Task<bool> ExistsAsync(string username, string email)
        {
            var sql = @"SELECT COUNT(*) FROM Users
                       WHERE (Username = @Username OR Email = @Email) AND IsActive = 1";

            var count = await _db.ExecuteScalarAsync<int>(sql, new Dictionary<string, object>
            {
                { "Username", username },
                { "Email", email }
            });

            return count > 0;
        }

        public async Task<List<User>> GetAllActiveUsersAsync()
        {
            var sql = "SELECT * FROM Users WHERE IsActive = 1 ORDER BY CreatedAt DESC";
            return await _db.QueryAsync(sql);
        }

        public async Task<int> GetCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
            return await _db.ExecuteScalarAsync<int>(sql);
        }

        // 통계 메서드 구현
        public async Task<int> GetTotalUsersCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
            return await _db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<int> GetActiveUsersCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND LockedUntil IS NULL";
            return await _db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<int> GetLockedUsersCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND LockedUntil IS NOT NULL AND LockedUntil > UTC_TIMESTAMP()";
            return await _db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<int> GetTodayRegistrationsCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND DATE(CreatedAt) = CURDATE()";
            return await _db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<int> GetTodayLoginsCountAsync()
        {
            var sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND DATE(LastLoginAt) = CURDATE()";
            return await _db.ExecuteScalarAsync<int>(sql);
        }
    }
}
