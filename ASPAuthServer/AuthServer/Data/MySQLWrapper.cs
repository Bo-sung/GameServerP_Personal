using MySql.Data.MySqlClient;
using System.Reflection;

namespace AuthServer.Data
{
    public class MySQLWrapper<T> where T : class, new()
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MySQLWrapper(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // 단일 결과 조회 (SELECT ... LIMIT 1)
        public async Task<T?> QueryFirstOrDefaultAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);

            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToObject(reader);
            }

            return null;
        }

        // 여러 결과 조회 (SELECT ...)
        public async Task<List<T>> QueryAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            var results = new List<T>();

            using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);

            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapToObject(reader));
            }

            return results;
        }

        // INSERT, UPDATE, DELETE (영향받은 행 수 반환)
        public async Task<int> ExecuteAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);

            return await command.ExecuteNonQueryAsync();
        }

        // COUNT, LAST_INSERT_ID 등 단일 값 반환
        public async Task<TResult> ExecuteScalarAsync<TResult>(string sql, Dictionary<string, object>? parameters = null)
        {
            using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);

            var result = await command.ExecuteScalarAsync();
            return (TResult)Convert.ChangeType(result!, typeof(TResult));
        }

        private void AddParameters(MySqlCommand command, Dictionary<string, object>? parameters)
        {
            if (parameters == null) return;

            foreach (var param in parameters)
            {
                var paramName = param.Key.StartsWith("@") ? param.Key : "@" + param.Key;
                command.Parameters.AddWithValue(paramName, param.Value ?? DBNull.Value);
            }
        }

        private T MapToObject(MySqlDataReader reader)
        {
            var obj = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var property = properties.FirstOrDefault(p => p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                if (property != null && property.CanWrite)
                {
                    var value = reader.GetValue(i);
                    if (value != DBNull.Value)
                    {
                        // Nullable 타입 처리
                        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        property.SetValue(obj, Convert.ChangeType(value, propertyType));
                    }
                }
            }

            return obj;
        }
    }
}
