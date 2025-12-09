using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis 연결 및 기본 관리 서비스
    /// Standalone, Sentinel, Cluster 모드 지원
    /// </summary>
    public class RedisService : IDisposable
    {
        private ConnectionMultiplexer? _connection;
        private readonly RedisConfig _config;
        private bool _isDisposed;

        public bool IsConnected => _connection?.IsConnected ?? false;
        public IDatabase Database => _connection?.GetDatabase() ?? throw new InvalidOperationException("Redis not connected");
        public ISubscriber Subscriber => _connection?.GetSubscriber() ?? throw new InvalidOperationException("Redis not connected");

        public RedisService(RedisConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Redis 연결 초기화
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_connection != null && _connection.IsConnected)
                {
                    return true;
                }

                var options = BuildConfigurationOptions();

                LogInfo($"[Redis] Connecting to {_config.Host}:{_config.Port}...");
                _connection = await ConnectionMultiplexer.ConnectAsync(options);

                _connection.ConnectionFailed += OnConnectionFailed;
                _connection.ConnectionRestored += OnConnectionRestored;
                _connection.ErrorMessage += OnErrorMessage;

                LogInfo($"[Redis] Connected successfully to {_config.Host}:{_config.Port}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[Redis] Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Redis 설정 옵션 빌드
        /// </summary>
        private ConfigurationOptions BuildConfigurationOptions()
        {
            var options = new ConfigurationOptions
            {
                ConnectTimeout = _config.ConnectTimeout,
                SyncTimeout = _config.SyncTimeout,
                AbortOnConnectFail = false,
                ConnectRetry = 3,
                KeepAlive = 60,
                DefaultDatabase = _config.Database,
                Ssl = _config.UseSSL,
                AllowAdmin = _config.AllowAdmin
            };

            // 엔드포인트 추가
            foreach (var endpoint in _config.Endpoints)
            {
                options.EndPoints.Add(endpoint.Host, endpoint.Port);
            }

            // 비밀번호 설정
            if (!string.IsNullOrEmpty(_config.Password))
            {
                options.Password = _config.Password;
            }

            // Sentinel 모드
            if (_config.Mode == RedisMode.Sentinel && _config.HighAvailability.Enabled)
            {
                options.TieBreaker = "";
                options.CommandMap = CommandMap.Sentinel;
                options.ServiceName = _config.HighAvailability.MasterName;
            }

            return options;
        }

        /// <summary>
        /// Health Check
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                if (!IsConnected)
                    return false;

                var db = Database;
                await db.PingAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Redis 정보 조회
        /// </summary>
        public async Task<string> GetInfoAsync()
        {
            try
            {
                if (!IsConnected)
                    return "Not connected";

                var server = _connection!.GetServer(_connection.GetEndPoints()[0]);
                var info = await server.InfoAsync();
                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 연결 실패 이벤트
        /// </summary>
        private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            LogError($"[Redis] Connection failed: {e.Exception?.Message ?? "Unknown error"}");
        }

        /// <summary>
        /// 연결 복구 이벤트
        /// </summary>
        private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            LogInfo($"[Redis] Connection restored");
        }

        /// <summary>
        /// 에러 메시지 이벤트
        /// </summary>
        private void OnErrorMessage(object? sender, RedisErrorEventArgs e)
        {
            LogError($"[Redis] Error: {e.Message}");
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _connection?.Close();
            _connection?.Dispose();
            _isDisposed = true;

            LogInfo("[Redis] Connection disposed");
        }
    }

    /// <summary>
    /// Redis 설정
    /// </summary>
    public class RedisConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 6379;
        public string Password { get; set; } = string.Empty;
        public int Database { get; set; } = 0;
        public int ConnectTimeout { get; set; } = 5000;
        public int SyncTimeout { get; set; } = 5000;
        public bool UseSSL { get; set; } = false;
        public bool AllowAdmin { get; set; } = false;
        public RedisMode Mode { get; set; } = RedisMode.Standalone;
        public RedisEndpoint[] Endpoints { get; set; } = Array.Empty<RedisEndpoint>();
        public HighAvailabilityConfig HighAvailability { get; set; } = new();
        public ClusteringConfig Clustering { get; set; } = new();
    }

    /// <summary>
    /// Redis 엔드포인트
    /// </summary>
    public class RedisEndpoint
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 6379;
    }

    /// <summary>
    /// 고가용성 설정 (Sentinel)
    /// </summary>
    public class HighAvailabilityConfig
    {
        public bool Enabled { get; set; }
        public RedisEndpoint[] Sentinels { get; set; } = Array.Empty<RedisEndpoint>();
        public string MasterName { get; set; } = "mymaster";
    }

    /// <summary>
    /// 클러스터링 설정
    /// </summary>
    public class ClusteringConfig
    {
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Redis 모드
    /// </summary>
    public enum RedisMode
    {
        Standalone,
        Sentinel,
        Cluster
    }
}
