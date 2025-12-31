using MySql.Data.MySqlClient;
using System.Data;

namespace AuthServer.Data
{
    /// <summary>
    /// DB 초기화 클래스 - Main 함수에서만 사용
    /// DI 컨테이너에 등록하지 않아야 함 (보안)
    /// </summary>
    internal sealed class DbInitializer
    {
        private readonly string _connectionString;

        public DbInitializer(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("연결 문자열이 비어있습니다.", nameof(connectionString));

            _connectionString = connectionString;
        }

        /// <summary>
        /// DB 초기화 - 테이블 확인 및 생성
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await EnsureDatabaseExistsAsync();
                await EnsureTablesExistAsync();
                await SeedDefaultDataAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[DB 초기화 실패] {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        /// <summary>
        /// 데이터베이스 존재 확인
        /// </summary>
        private async Task EnsureDatabaseExistsAsync()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.Database;

            // Database 없이 연결
            builder.Database = "";
            var masterConnectionString = builder.ConnectionString;

            using var connection = new MySqlConnection(masterConnectionString);
            await connection.OpenAsync();

            var checkDbSql = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{databaseName}'";
            using var checkCmd = new MySqlCommand(checkDbSql, connection);
            var exists = await checkCmd.ExecuteScalarAsync();

            if (exists == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  → 데이터베이스 '{databaseName}' 생성 중...");
                Console.ResetColor();

                var createDbSql = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
                using var createCmd = new MySqlCommand(createDbSql, connection);
                await createCmd.ExecuteNonQueryAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ 데이터베이스 '{databaseName}' 생성 완료");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ✓ 데이터베이스 '{databaseName}' 존재 확인");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 필요한 테이블들이 존재하는지 확인하고 없으면 생성
        /// </summary>
        private async Task EnsureTablesExistAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Users 테이블 확인 및 생성
            await EnsureUsersTableAsync(connection);

            // Admins 테이블 확인 및 생성
            await EnsureAdminsTableAsync(connection);
        }

        /// <summary>
        /// Users 테이블 확인 및 생성
        /// </summary>
        private async Task EnsureUsersTableAsync(MySqlConnection connection)
        {
            var tableName = "Users";

            if (!await TableExistsAsync(connection, tableName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  → 테이블 '{tableName}' 생성 중...");
                Console.ResetColor();

                var createTableSql = @"
                    CREATE TABLE Users (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Username VARCHAR(50) NOT NULL UNIQUE,
                        Email VARCHAR(100) NOT NULL UNIQUE,
                        PasswordHash VARCHAR(255) NOT NULL,
                        CreatedAt DATETIME NOT NULL,
                        LastLoginAt DATETIME NULL,
                        IsActive TINYINT(1) NOT NULL DEFAULT 1,
                        LoginAttempts INT NOT NULL DEFAULT 0,
                        LockedUntil DATETIME NULL,
                        INDEX idx_username (Username),
                        INDEX idx_email (Email),
                        INDEX idx_isactive (IsActive)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                ";

                using var command = new MySqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ 테이블 '{tableName}' 생성 완료");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ✓ 테이블 '{tableName}' 존재 확인");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Admins 테이블 확인 및 생성
        /// </summary>
        private async Task EnsureAdminsTableAsync(MySqlConnection connection)
        {
            var tableName = "Admins";

            if (!await TableExistsAsync(connection, tableName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  → 테이블 '{tableName}' 생성 중...");
                Console.ResetColor();

                var createTableSql = @"
                    CREATE TABLE Admins (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Username VARCHAR(50) NOT NULL UNIQUE,
                        Email VARCHAR(100) NOT NULL UNIQUE,
                        PasswordHash VARCHAR(255) NOT NULL,
                        CreatedAt DATETIME NOT NULL,
                        LastLoginAt DATETIME NULL,
                        IsActive TINYINT(1) NOT NULL DEFAULT 1,
                        LoginAttempts INT NOT NULL DEFAULT 0,
                        LockedUntil DATETIME NULL,
                        Role VARCHAR(50) NOT NULL DEFAULT 'Admin',
                        Permissions TEXT NULL,
                        INDEX idx_username (Username),
                        INDEX idx_email (Email),
                        INDEX idx_isactive (IsActive),
                        INDEX idx_role (Role)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                ";

                using var command = new MySqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ 테이블 '{tableName}' 생성 완료");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ✓ 테이블 '{tableName}' 존재 확인");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 테이블 존재 여부 확인
        /// </summary>
        private async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = @TableName
            ";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", tableName);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// 기본 데이터 시딩
        /// </summary>
        private async Task SeedDefaultDataAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Users 테이블 시딩
            await SeedUsersTableAsync(connection);

            // Admins 테이블 시딩
            await SeedAdminsTableAsync(connection);
        }

        /// <summary>
        /// Users 테이블 기본 데이터 시딩
        /// </summary>
        private async Task SeedUsersTableAsync(MySqlConnection connection)
        {
            var countSql = "SELECT COUNT(*) FROM Users";
            using var countCmd = new MySqlCommand(countSql, connection);
            var userCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            if (userCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  → 기본 테스트 사용자 계정 생성 중...");
                Console.ResetColor();

                var passwordHash = HashPassword("user123");
                var insertSql = @"
                    INSERT INTO Users (Username, Email, PasswordHash, CreatedAt, IsActive, LoginAttempts)
                    VALUES (@Username, @Email, @PasswordHash, @CreatedAt, @IsActive, @LoginAttempts)
                ";

                using var insertCmd = new MySqlCommand(insertSql, connection);
                insertCmd.Parameters.AddWithValue("@Username", "testuser");
                insertCmd.Parameters.AddWithValue("@Email", "testuser@example.com");
                insertCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                insertCmd.Parameters.AddWithValue("@IsActive", true);
                insertCmd.Parameters.AddWithValue("@LoginAttempts", 0);

                await insertCmd.ExecuteNonQueryAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ 기본 테스트 사용자 계정 생성 완료");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("     Username: testuser");
                Console.WriteLine("     Password: user123");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ✓ 기존 사용자 확인 ({userCount}명)");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Admins 테이블 기본 데이터 시딩
        /// </summary>
        private async Task SeedAdminsTableAsync(MySqlConnection connection)
        {
            var countSql = "SELECT COUNT(*) FROM Admins";
            using var countCmd = new MySqlCommand(countSql, connection);
            var adminCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            if (adminCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  → 기본 관리자 계정 생성 중...");
                Console.ResetColor();

                // 기본 관리자 계정 생성 (admin / admin123)
                var passwordHash = HashPassword("admin123");
                var insertSql = @"
                    INSERT INTO Admins (Username, Email, PasswordHash, CreatedAt, IsActive, LoginAttempts, Role, Permissions)
                    VALUES (@Username, @Email, @PasswordHash, @CreatedAt, @IsActive, @LoginAttempts, @Role, @Permissions)
                ";

                using var insertCmd = new MySqlCommand(insertSql, connection);
                insertCmd.Parameters.AddWithValue("@Username", "admin");
                insertCmd.Parameters.AddWithValue("@Email", "admin@example.com");
                insertCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                insertCmd.Parameters.AddWithValue("@IsActive", true);
                insertCmd.Parameters.AddWithValue("@LoginAttempts", 0);
                insertCmd.Parameters.AddWithValue("@Role", "SuperAdmin");
                insertCmd.Parameters.AddWithValue("@Permissions", DBNull.Value); // 모든 권한

                await insertCmd.ExecuteNonQueryAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ 기본 관리자 계정 생성 완료");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("     Username: admin");
                Console.WriteLine("     Password: admin123");
                Console.WriteLine("     Role: SuperAdmin");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("     ⚠️  보안을 위해 비밀번호를 즉시 변경하세요!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ✓ 기존 관리자 확인 ({adminCount}명)");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 비밀번호 해싱 (AuthService와 동일한 로직)
        /// </summary>
        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();

            // SecuritySettings의 기본값 사용 (1000 iterations)
            var iterations = 1000;
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);

            for (int i = 0; i < iterations; i++)
            {
                bytes = sha256.ComputeHash(bytes);
            }

            return Convert.ToBase64String(bytes);
        }
    }
}
