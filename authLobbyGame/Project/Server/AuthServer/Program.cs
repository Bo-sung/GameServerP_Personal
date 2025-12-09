using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer
{
    /// <summary>
    /// Auth 서버 프로그램 진입점
    /// JWT 기반 인증 서버를 설정하고 실행
    /// </summary>
    class Program
    {
        /// <summary>
        /// 서버 메인 진입점
        /// JWT 인증 설정, 컨트롤러 등록, 서버 시작
        /// </summary>
        /// <param name="args">명령줄 인자</param>
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    Auth Server Starting...");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var builder = WebApplication.CreateBuilder(args);

            // 데이터베이스 설정 (구조화된 방식)
            var dbConfig = builder.Configuration.GetSection("Database:Auth");
            string dbServer = dbConfig.GetValue<string>("Server") ?? "localhost";
            int dbPort = dbConfig.GetValue<int>("Port");
            if (dbPort == 0) dbPort = 3306;
            string dbName = dbConfig.GetValue<string>("DatabaseName") ?? "authdb";
            string dbUserId = dbConfig.GetValue<string>("UserId") ?? "root";
            string dbPassword = dbConfig.GetValue<string>("Password") ?? "";

            // ConnectionString 조립
            string connectionString = $"Server={dbServer};Port={dbPort};Database={dbName};User={dbUserId};Password={dbPassword};";

            Console.WriteLine($"[Config] Database: {dbServer}:{dbPort}/{dbName}");
            Console.WriteLine($"[Config] User: {dbUserId}");

            // JWT 설정
            var jwtSection = builder.Configuration.GetSection("Jwt");
            var key = jwtSection.GetValue<string>("Key") ?? "please_change_this_secret_in_appsettings";
            var issuer = jwtSection.GetValue<string>("Issuer") ?? "AuthServer";
            var audience = jwtSection.GetValue<string>("Audience") ?? "AuthClient";

            Console.WriteLine($"[Config] JWT Issuer: {issuer}");
            Console.WriteLine($"[Config] JWT Audience: {audience}");

            // 서버 레지스트리 설정
            string serverSecretKey = builder.Configuration.GetValue<string>("ServerRegistry:SecretKey") ?? "default_secret";
            Console.WriteLine($"[Config] Server Registry initialized");
            Console.WriteLine();

            builder.Services.AddControllers();

            // 서비스 등록
            builder.Services.AddSingleton<Services.ServerRegistryService>(sp => new Services.ServerRegistryService(serverSecretKey));

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // 배포 시 true로 변경 권장
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            });

            builder.Services.AddSingleton<Services.JwtService>(sp => new Services.JwtService(key, issuer, audience));

            var app = builder.Build();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            Console.WriteLine("[Auth Server] Listening on configured endpoints...");
            Console.WriteLine();

            await app.RunAsync();
        }
    }
}
