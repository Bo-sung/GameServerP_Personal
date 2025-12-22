using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuthServer.Models;
using AuthServer.Settings;

namespace AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettings;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IOptionsSnapshot<JwtSettings> jwtSettings,
            ILogger<AdminController> logger)
        {
            _jwtSettings = jwtSettings;
            _logger = logger;
        }

        // POST: api/admin/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Admin login request for username: {Username}", request.Username);

            var adminSettings = _jwtSettings.Get("Admin");

            // TODO: 관리자 로그인 로직 구현
            // 1. 관리자 계정 인증
            // 2. Admin JWT 토큰 생성

            return Ok(new
            {
                message = "Admin login endpoint ready",
                audience = adminSettings.Audience,
                expiresIn = adminSettings.AccessTokenExpirationMinutes
            });
        }

        // GET: api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation("Get all users request - Page: {Page}, PageSize: {PageSize}", page, pageSize);

            // TODO: 사용자 목록 조회 로직 구현
            // 관리자 권한 검증 필요

            return Ok(new
            {
                message = "Get users endpoint ready",
                page,
                pageSize
            });
        }

        // DELETE: api/admin/users/{userId}
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            _logger.LogInformation("Delete user request for userId: {UserId}", userId);

            // TODO: 사용자 삭제 로직 구현
            // 관리자 권한 검증 필요

            return Ok(new
            {
                message = "Delete user endpoint ready",
                userId
            });
        }
    }
}
