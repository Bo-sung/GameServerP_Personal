using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace CommonLib.Database
{
    /// <summary>
    /// 통합 MySQL DB 헬퍼 - 모든 서버에서 공통 사용
    /// </summary>
    public static class MySqlHelper
    {
        public static async Task<MySqlConnection> OpenConnectionAsync(string connectionString)
        {
            var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public static async Task<int> ExecuteNonQueryAsync(string connectionString, string query, params MySqlParameter[] parameters)
        {
            using var conn = await OpenConnectionAsync(connectionString);
            using var cmd = new MySqlCommand(query, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);
            return await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<object?> ExecuteScalarAsync(string connectionString, string query, params MySqlParameter[] parameters)
        {
            using var conn = await OpenConnectionAsync(connectionString);
            using var cmd = new MySqlCommand(query, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);
            return await cmd.ExecuteScalarAsync();
        }

        public static async Task<MySqlDataReader> ExecuteReaderAsync(string connectionString, string query, params MySqlParameter[] parameters)
        {
            var conn = await OpenConnectionAsync(connectionString);
            var cmd = new MySqlCommand(query, conn);
            if (parameters != null)
                cmd.Parameters.AddRange(parameters);
            // Caller must dispose reader and connection
            return await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        }
    }
}
