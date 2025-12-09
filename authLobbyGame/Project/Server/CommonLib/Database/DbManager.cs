using CommonLib.Database;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace CommonLib.Database
{
    /// <summary>
    /// 공통 DB 접근 매니저 (기본 기능)
    /// </summary>
    public class DbManager
    {
        protected readonly string _connectionString;

        public DbManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
        {
            return await MySqlHelper.ExecuteNonQueryAsync(_connectionString, query, parameters);
        }

        public async Task<object?> ExecuteScalarAsync(string query, params MySqlParameter[] parameters)
        {
            return await MySqlHelper.ExecuteScalarAsync(_connectionString, query, parameters);
        }

        public async Task<MySqlDataReader> ExecuteReaderAsync(string query, params MySqlParameter[] parameters)
        {
            return await MySqlHelper.ExecuteReaderAsync(_connectionString, query, parameters);
        }
    }
}
