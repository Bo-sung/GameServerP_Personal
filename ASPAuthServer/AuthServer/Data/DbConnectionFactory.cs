using MySql.Data.MySqlClient;
using System.Data;
using Microsoft.Extensions.Options;
using AuthServer.Settings;

namespace AuthServer.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly DatabaseSettings _settings;

        public DbConnectionFactory(IOptions<DatabaseSettings> settings)
        {
            _settings = settings.Value;
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnection(_settings.MySQLConnection);
        }
    }
}
