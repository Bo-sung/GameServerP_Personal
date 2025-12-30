using AuthServer.Settings;
using AuthServer.Data;
using AuthServer.Data.Repositories;
using AuthServer.Services.Auth;
using AuthServer.Services.Tokens;
using Microsoft.Extensions.Options;

namespace AuthServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controller 등록
            builder.Services.AddControllers();

            // Options 패턴으로 설정 등록 및 검증
            builder.Services.AddOptions<DatabaseSettings>()
                .Bind(builder.Configuration.GetSection("DatabaseSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Named Options로 여러 JwtSettings 등록 (Game과 Admin 분리)
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

            // DB 관련 서비스 등록
            builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            builder.Services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // 비즈니스 서비스 등록
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            var app = builder.Build();

            // MySQL 연결 테스트
            if (!await TestMySQLConnectionAsync(app.Services))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[Fatal Error] MySQL 연결 실패!");
                Console.WriteLine("데이터베이스 서버가 실행 중인지 확인하세요.");
                Console.WriteLine("appsettings.json의 ConnectionString 설정을 확인하세요.");
                Console.ResetColor();
                Environment.Exit(1);
                return;
            }

            // Redis 연결 테스트
            if (!await TestRedisConnectionAsync(app.Services))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[Fatal Error] Redis 연결 실패!");
                Console.WriteLine("Redis 서버가 실행 중인지 확인하세요.");
                Console.WriteLine("appsettings.json의 Redis ConnectionString 설정을 확인하세요.");
                Console.ResetColor();
                Environment.Exit(1);
                return;
            }

            // DB 초기화 (테이블 생성 및 시드 데이터)
            await InitializeDatabaseAsync(app.Services);

            // Controller 라우팅 활성화
            app.MapControllers();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[AuthServer] 서버가 성공적으로 시작되었습니다.");
            Console.ResetColor();

            // 서버 리스닝 주소 출력
            var urls = app.Urls;
            if (urls.Any())
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n서버 리스닝 중:");
                foreach (var url in urls)
                {
                    Console.WriteLine($"  - {url}");
                }
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n서버 리스닝 중: http://localhost:5126");
                Console.ResetColor();
            }

            Console.WriteLine("\n서버를 중지하려면 Ctrl+C를 누르세요.\n");

            await app.RunAsync();
        }

        /// <summary>
        /// MySQL 연결 테스트 (데이터베이스 이름 제외하고 서버 연결만 확인)
        /// </summary>
        private static async Task<bool> TestMySQLConnectionAsync(IServiceProvider services)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbSettings = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

                // 데이터베이스 이름을 제외한 연결 문자열 생성 (아직 DB가 없을 수 있음)
                var connectionStringWithoutDb = RemoveDatabaseFromConnectionString(dbSettings.MySQLConnection);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[MySQL] 연결 시도 중... (Server={ExtractServerFromConnectionString(dbSettings.MySQLConnection)})");
                Console.ResetColor();

                using var connection = new MySql.Data.MySqlClient.MySqlConnection(connectionStringWithoutDb);
                connection.Open();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[MySQL] 연결 성공");
                Console.ResetColor();

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[MySQL] 연결 실패: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>
        /// ConnectionString에서 Database 부분을 제거 (서버 연결 테스트용)
        /// </summary>
        private static string RemoveDatabaseFromConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';')
                .Where(p => !p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return string.Join(";", parts);
        }

        /// <summary>
        /// ConnectionString에서 Server 정보 추출
        /// </summary>
        private static string ExtractServerFromConnectionString(string connectionString)
        {
            try
            {
                var parts = connectionString.Split(';');
                var serverPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Server=", StringComparison.OrdinalIgnoreCase));
                if (serverPart != null)
                {
                    return serverPart.Split('=')[1].Trim();
                }
                return "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Redis 연결 테스트
        /// </summary>
        private static async Task<bool> TestRedisConnectionAsync(IServiceProvider services)
        {
            try
            {
                using var scope = services.CreateScope();
                var redisFactory = scope.ServiceProvider.GetRequiredService<IRedisConnectionFactory>();

                var db = redisFactory.GetDatabase();
                await db.PingAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Redis] 연결 성공");
                Console.ResetColor();

                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Redis] 연결 실패: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>
        /// 데이터베이스 초기화 (테이블 생성 및 시드 데이터)
        /// </summary>
        private static async Task InitializeDatabaseAsync(IServiceProvider services)
        {
            try
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("  데이터베이스 초기화 시작");
                Console.WriteLine("========================================");

                var config = services.GetRequiredService<IConfiguration>();
                var connectionString = config.GetSection("DatabaseSettings:MySQLConnection").Value;

                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[DB 초기화] MySQLConnection이 설정되지 않았습니다.");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine("\n[1단계] 데이터베이스 확인 및 생성");
                Console.WriteLine("[2단계] 테이블 스키마 확인 및 생성");
                Console.WriteLine("[3단계] 기본 데이터 시딩\n");

                var dbInitializer = new DbInitializer(connectionString);
                await dbInitializer.InitializeAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ 데이터베이스 초기화 완료");
                Console.ResetColor();
                Console.WriteLine("========================================\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ [DB 초기화 실패] {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\n수동 설정 방법:");
                Console.WriteLine("1. DOCS/setup_database.sql 파일을 확인하세요");
                Console.WriteLine("2. MySQL 클라이언트로 접속: mysql -u root -p");
                Console.WriteLine("3. SQL 스크립트 실행: source DOCS/setup_database.sql");
                Console.WriteLine("========================================\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("경고: 데이터베이스 초기화에 실패했지만 서버는 계속 실행됩니다.");
                Console.WriteLine("일부 기능이 정상적으로 작동하지 않을 수 있습니다.\n");
                Console.ResetColor();
            }
        }
    }
}
