using CommonLib.Database;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace CommonLib.Database
{
    /// <summary>
    /// 인증 DB 전용 매니저 (확장)
    /// </summary>
    public class AuthDbManager : DbManager
    {
        public AuthDbManager(string connectionString) : base(connectionString) { }

        // 예시: 사용자 인증
        public async Task<UserInfo?> AuthenticateUserAsync(string username, string password)
        {
            string query = "SELECT id, name, password FROM userinfo WHERE name = @name";
            var param = new MySqlParameter("@name", username);
            using var reader = await ExecuteReaderAsync(query, param);
            if (await reader.ReadAsync())
            {
                int id = reader.GetInt32("id");
                string name = reader.GetString("name");
                string stored = reader.IsDBNull(reader.GetOrdinal("password")) ? string.Empty : reader.GetString("password");
                // ...비밀번호 검증 로직...
                return new UserInfo { UserId = id, UserName = name };
            }
            return null;
        }

        // 추가 인증 관련 메서드 구현 가능
    }
}
