using CommonLib;
using CommonLib.Database;
using MySql.Data.MySqlClient;
using BCrypt.Net;

namespace LobbyServer.Database
{
    /// <summary>
    /// LobbyServer용 인증 DB 매니저 (CommonLib.AuthDbManager를 상속)
    /// 추가적인 비즈니스 로직이 필요한 경우 여기에 구현
    /// </summary>
    public sealed class DB_Auth : AuthDbManager
    {
        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }

        public DB_Auth() : base(CommonLib.AppConfig.Instance.AuthDatabaseConnectionString)
        {
        }

        /// <summary>
        /// 로그인 인증 - userinfo 테이블에서 사용자 확인
        /// - 새로 가입하는 사용자는 BCrypt 해시로 저장합니다.
        /// - 기존 DB에 평문 비밀번호가 있는 경우, 로그인 성공 시 해시로 자동 업그레이드합니다.
        /// </summary>
        public UserInfo? AuthenticateUser(string username, string password)
        {
            try
            {
                string query = "SELECT id, name, password FROM userinfo WHERE name = @name";
                var param = new MySqlParameter("@name", username);

                var result = ExecuteReaderAsync(query, param).Result;
                using (result)
                {
                    if (result.Read())
                    {
                        int id = result.GetInt32("id");
                        string name = result.GetString("name");
                        string stored = result.IsDBNull(result.GetOrdinal("password")) ? string.Empty : result.GetString("password");

                        bool verified = false;

                        // 1) 저장된 값과 평문이 동일한 경우 (구버전/마이그레이션 대상)
                        if (!string.IsNullOrEmpty(stored) && stored == password)
                        {
                            verified = true;

                            try
                            {
                                string newHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
                                result.Close();

                                ExecuteNonQueryAsync("UPDATE userinfo SET password = @hash WHERE id = @id",
                                    new MySqlParameter("@hash", newHash),
                                    new MySqlParameter("@id", id)).Wait();
                            }
                            catch (Exception ex)
                            {
                                LogWithTimestamp($"[DB_Auth] Password upgrade failed for user {name}: {ex.Message}");
                            }
                        }
                        else if (!string.IsNullOrEmpty(stored))
                        {
                            // 2) 해시 검증 시도
                            try
                            {
                                verified = BCrypt.Net.BCrypt.Verify(password, stored);
                            }
                            catch
                            {
                                verified = false;
                            }
                        }

                        if (verified)
                        {
                            return new UserInfo
                            {
                                UserId = id,
                                UserName = name
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[DB_Auth] AuthenticateUser Error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 회원가입 - userinfo 테이블에 새 사용자 추가
        /// - 비밀번호는 BCrypt 해시로 저장합니다.
        /// </summary>
        public bool RegisterUser(string username, string password)
        {
            try
            {
                // 중복 체크 먼저
                string checkQuery = "SELECT COUNT(*) FROM userinfo WHERE name = @name";
                var checkParam = new MySqlParameter("@name", username);
                long count = (long)(ExecuteScalarAsync(checkQuery, checkParam).Result ?? 0);
                if (count > 0)
                {
                    LogWithTimestamp($"[DB_Auth] User '{username}' already exists");
                    return false;
                }

                // 비밀번호 해시 생성
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

                // 사용자 추가 (해시 저장)
                string insertQuery = "INSERT INTO userinfo (name, password) VALUES (@name, @password)";
                int rowsAffected = ExecuteNonQueryAsync(insertQuery,
                    new MySqlParameter("@name", username),
                    new MySqlParameter("@password", hashedPassword)).Result;
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[DB_Auth] RegisterUser Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 유저 존재 여부 확인
        /// </summary>
        public bool UserExists(string username)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM userinfo WHERE name = @name";
                var param = new MySqlParameter("@name", username);
                long count = (long)(ExecuteScalarAsync(query, param).Result ?? 0);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[DB_Auth] UserExists Error: {ex.Message}");
                return false;
            }
        }
    }
}
