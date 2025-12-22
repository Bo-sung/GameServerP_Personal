using StackExchange.Redis;
using Microsoft.Extensions.Options;
using AuthServer.Settings;

namespace AuthServer.Data
{
    public interface IRedisConnectionFactory
    {
        IDatabase GetDatabase();
        IConnectionMultiplexer GetConnection();
    }

    public class RedisConnectionFactory : IRedisConnectionFactory, IDisposable
    {
        private readonly ConnectionMultiplexer _connection;
        private bool _disposed = false;

        public RedisConnectionFactory(IOptions<DatabaseSettings> settings)
        {
            _connection = ConnectionMultiplexer.Connect(settings.Value.RedisConnection);
        }

        public IDatabase GetDatabase()
        {
            return _connection.GetDatabase();
        }

        public IConnectionMultiplexer GetConnection()
        {
            return _connection;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
