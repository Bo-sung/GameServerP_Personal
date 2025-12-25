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

            // Controller 라우팅 활성화
            app.MapControllers();

            await app.RunAsync();
        }
    }
}
