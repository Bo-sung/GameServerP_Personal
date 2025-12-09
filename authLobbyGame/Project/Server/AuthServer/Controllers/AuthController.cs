using System;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using CommonLib.Database;
using AuthServer.Services;
using AuthServer.Models;

namespace AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly JwtService _jwtService;
        private readonly ServerRegistryService _serverRegistry;

        /// <summary>
        /// 생성자 - 설정, JWT 서비스, 서버 레지스트리 주입
        /// </summary>
        public AuthController(IConfiguration config, JwtService jwtService, ServerRegistryService serverRegistry)
        {
            _config = config;
            _jwtService = jwtService;
            _serverRegistry = serverRegistry;
        }

        /// <summary>
        /// 인증 데이터베이스 매니저 생성
        /// 연결 문자열을 구조화된 설정에서 가져와 조립 후 AuthDbManager 인스턴스를 반환
        /// </summary>
        private AuthDbManager GetAuthDbManager()
        {
            // 구조화된 Database 설정 읽기
            var dbConfig = _config.GetSection("Database:Auth");
            string server = dbConfig.GetValue<string>("Server") ?? "localhost";
            int port = dbConfig.GetValue<int>("Port");
            if (port == 0) port = 3306;
            string database = dbConfig.GetValue<string>("DatabaseName") ?? "authdb";
            string userId = dbConfig.GetValue<string>("UserId") ?? "root";
            string password = dbConfig.GetValue<string>("Password") ?? "";

            // ConnectionString 조립
            string connStr = $"Server={server};Port={port};Database={database};User={userId};Password={password};";

            return new AuthDbManager(connStr);
        }

        /// <summary>
        /// 사용자 로그인 처리
        /// - 사용자명과 비밀번호로 인증
        /// - 성공 시 Access Token과 Refresh Token 발급
        /// - Refresh Token은 DB에 해시 형태로 저장 (30일 유효)
        /// </summary>
        /// <param name="req">로그인 요청 (username, password)</param>
        /// <returns>토큰 및 사용자 정보 포함 응답</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new { success = false, message = "Invalid request" });

            try
            {
                var db = GetAuthDbManager();
                // DB에서 사용자 인증 (BCrypt 해시 검증 포함)
                var user = await db.AuthenticateUserAsync(req.Username, req.Password);
                if (user == null)
                    return Unauthorized(new { success = false, message = "Invalid username or password" });

                // Access Token 생성 (60분 유효)
                string accessToken = _jwtService.GenerateAccessToken(user.Value.UserId.ToString(), expiresMinutes: 60);
                // Refresh Token 생성 (무작위 문자열)
                string refreshToken = Guid.NewGuid().ToString("N");

                // Refresh Token을 SHA256 해시로 변환하여 DB에 저장
                try
                {
                    string refreshHash = ComputeSha256Hash(refreshToken);
                    await db.ExecuteNonQueryAsync("INSERT INTO refresh_tokens (user_id, token_hash, expires_at) VALUES (@uid, @hash, @exp)",
                        new MySql.Data.MySqlClient.MySqlParameter("@uid", user.Value.UserId),
                        new MySql.Data.MySqlClient.MySqlParameter("@hash", refreshHash),
                        new MySql.Data.MySqlClient.MySqlParameter("@exp", DateTime.UtcNow.AddDays(30)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to store refresh token: {ex.Message}");
                }

                // 추천 Lobby 서버 조회
                var recommendedLobby = _serverRegistry.GetRecommendedLobbyServer();

                return Ok(new
                {
                    success = true,
                    accessToken,
                    refreshToken,
                    expiresIn = 3600,
                    user = new { userId = user.Value.UserId, username = user.Value.UserName },
                    lobbyServer = recommendedLobby != null ? new
                    {
                        serverName = recommendedLobby.ServerName,
                        host = recommendedLobby.Host,
                        port = recommendedLobby.Port,
                        currentPlayers = recommendedLobby.CurrentPlayers,
                        maxCapacity = recommendedLobby.MaxCapacity
                    } : null
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// JWT 토큰 검증
        /// - 제공된 Access Token의 유효성 확인
        /// - 유효한 토큰이면 사용자 ID 반환
        /// </summary>
        /// <param name="req">검증 요청 (token)</param>
        /// <returns>토큰 유효 여부 및 사용자 ID</returns>
        [HttpPost("verify")]
        public IActionResult Verify([FromBody] Models.VerifyRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Token))
                return BadRequest(new { valid = false });

            // JWT 토큰 검증 및 클레임 추출
            var principal = _jwtService.ValidateToken(req.Token);
            if (principal == null)
                return Unauthorized(new { valid = false });

            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Ok(new { valid = true, userId });
        }

        /// <summary>
        /// Refresh Token을 이용한 새로운 Access Token 발급
        /// - Refresh Token이 DB에 저장된 해시와 일치하는지 확인
        /// - 토큰이 아직 유효 기간 내인지 확인
        /// - 폐기되지 않았는지 확인
        /// </summary>
        /// <param name="req">토큰 갱신 요청 (refreshToken)</param>
        /// <returns>새로운 Access Token</returns>
        [HttpPost("refresh")]
        public IActionResult Refresh([FromBody] Models.RefreshRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.RefreshToken))
                return BadRequest(new { success = false });

            try
            {
                var db = GetAuthDbManager();
                // 제공된 Refresh Token을 해시로 변환
                string providedHash = ComputeSha256Hash(req.RefreshToken);
                // DB에서 토큰 정보 조회
                using var reader = db.ExecuteReaderAsync("SELECT user_id, expires_at, revoked FROM refresh_tokens WHERE token_hash = @hash",
                    new MySql.Data.MySqlClient.MySqlParameter("@hash", providedHash)).Result;
                if (!reader.Read())
                    return Unauthorized(new { success = false, message = "Invalid refresh token" });

                var userId = reader.GetInt32("user_id");
                var expires = reader.GetDateTime("expires_at");
                var revoked = reader.IsDBNull(reader.GetOrdinal("revoked")) ? false : reader.GetBoolean("revoked");

                // 토큰 유효성 확인 (만료 또는 폐기 여부)
                if (revoked || expires < DateTime.UtcNow)
                    return Unauthorized(new { success = false, message = "Invalid refresh token" });

                // 새로운 Access Token 발급
                string accessToken = _jwtService.GenerateAccessToken(userId.ToString(), expiresMinutes: 60);
                return Ok(new { success = true, accessToken, expiresIn = 3600 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Refresh error: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// 문자열을 SHA256 해시로 변환
        /// Refresh Token 저장 시 보안을 위해 해시로 저장
        /// </summary>
        /// <param name="rawData">원본 문자열</param>
        /// <returns>SHA256 해시 값 (16진수 문자열)</returns>
        private static string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
