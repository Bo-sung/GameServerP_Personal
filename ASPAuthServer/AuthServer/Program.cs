using AuthServer.Settings;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AuthServer
{
    public class DBManager
    {
        private readonly DatabaseSettings _dbSettings;
        public DBManager(IOptions<DatabaseSettings> dbSettings)
        {
            _dbSettings = dbSettings.Value;
        }
        // DB 연결 및 관리 로직 구현

        public class MySQL
        {
            public string MySQLConnection { get; set; } = string.Empty;

            [Range(1, 100)]
            public int MySQLConnectionPoolSize { get; set; } = 10;

            public bool EnableConnectionRetry { get; set; } = true;

            [Range(1, 10)]
            public int MaxRetryAttempts { get; set; } = 3;

            public MySQL(string connection, int poolSize, bool enableRetry, int maxRetries)
            {
                MySQLConnection = connection;
                MySQLConnectionPoolSize = poolSize;
                EnableConnectionRetry = enableRetry;
                MaxRetryAttempts = maxRetries;
            }

            // MySQL 연결 및 관리 로직 구현

            public bool TryConnect()
            {
                // MySQL 연결 로직

                return false; // 연결 실패 시 false 반환
            }
        }
    }
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controller 등록
            builder.Services.AddControllers();
            // Options 패턴으로 설정 등록 및 검증
            builder.Services.AddOptions<DatabaseSettings>()
                .Bind(builder.Configuration.GetSection("DatabaseSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Named Options로 여러 JwtSettings 등록
            builder.Services.AddOptions<JwtSettings>("Game")
                .Bind(builder.Configuration.GetSection("GameJwtSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<JwtSettings>("Admin")
                .Bind(builder.Configuration.GetSection("AdminJwtSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<SecuritySettings>()
                .Bind(builder.Configuration.GetSection("SecuritySettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<SessionSettings>()
                .Bind(builder.Configuration.GetSection("SessionSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<RateLimitSettings>()
                .Bind(builder.Configuration.GetSection("RateLimitSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var app = builder.Build();

            // Controller 라우팅 활성화
            app.MapControllers();

            app.MapGet("/", () => "Auth Server is running");

            app.Run();
        }
    }
}
