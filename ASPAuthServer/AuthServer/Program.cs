using AuthServer.Settings;
using AuthServer.Data;
using AuthServer.Data.Repositories;

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

            // DB 관련 서비스 등록
            builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            var app = builder.Build();

            // DB 초기화 (Main 함수에서만 실행)
            await InitializeDatabaseAsync(app);

            // Controller 라우팅 활성화
            app.MapControllers();

            //app.MapGet("/", () => "Auth Server is running");

            await app.RunAsync();
        }

        /// <summary>
        /// DB 초기화 - Main 함수에서만 호출
        /// </summary>
        private static async Task InitializeDatabaseAsync(WebApplication app)
        {
            try
            {
                // DatabaseSettings에서 연결 문자열 가져오기
                var dbSettings = app.Configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>();

                if (dbSettings == null || string.IsNullOrWhiteSpace(dbSettings.MySQLConnection))
                {
                    Console.WriteLine("[경고] DatabaseSettings가 설정되지 않았습니다. DB 초기화를 건너뜁니다.");
                    return;
                }

                // DbInitializer는 DI에 등록되지 않음 - Main에서만 직접 생성
                var initializer = new DbInitializer(dbSettings.MySQLConnection);
                await initializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 초기화 오류] {ex.Message}");
                Console.WriteLine("[경고] DB 초기화에 실패했지만 서버는 계속 실행됩니다.");
                Console.WriteLine($"상세 오류: {ex}");
                // DB 초기화 실패 시에도 서버는 시작되도록 예외를 throw하지 않음
            }
        }
    }
}
