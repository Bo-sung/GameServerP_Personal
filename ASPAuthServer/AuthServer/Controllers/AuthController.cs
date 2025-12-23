using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuthServer.Models;
using AuthServer.Settings;
using AuthServer.Services;
using AuthServer.Data.Repositories;

namespace AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository;
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IUserRepository userRepository,
            IOptionsSnapshot<JwtSettings> jwtSettings,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _userRepository = userRepository;
            _jwtSettings = jwtSettings;
            _logger = logger;
        }

        // GET: api/auth/users/{id}
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            _logger.LogInformation("Get user by id: {UserId}", id);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            return Ok(new UserInfoResponse(
                UserId: user.Id.ToString(),
                Username: user.Username,
                Email: user.Email,
                CreatedAt: user.CreatedAt
            ));
        }

        // POST: api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            _logger.LogInformation("Register request for username: {Username}", request.Username);

            var (success, userId, message) = await _authService.RegisterAsync(
                request.Username,
                request.Email ?? string.Empty,
                request.Password
            );

            if (!success)
            {
                return BadRequest(new ErrorResponse("REGISTER_FAILED", message ?? "회원가입 실패"));
            }

            // RESTful: 생성된 리소스 조회 URL을 Location 헤더에 포함
            return CreatedAtAction(
                nameof(GetUserById),
                new { id = userId },
                new
                {
                    userId,
                    username = request.Username,
                    message
                }
            );
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login request for username: {Username}", request.Username);

            var (success, token, message, user) = await _authService.LoginAsync(
                request.Username,
                request.Password
            );

            if (!success)
            {
                return Unauthorized(new ErrorResponse("LOGIN_FAILED", message ?? "로그인 실패"));
            }

            var gameSettings = _jwtSettings.Get("Game");

            return Ok(new AuthResponse(
                AccessToken: token!,
                RefreshToken: token!,
                ExpiresIn: gameSettings.AccessTokenExpirationMinutes * 60
            ));
        }

        // POST: api/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            _logger.LogInformation("Token refresh request");

            // TODO: 토큰 갱신 로직 구현
            // 1. Refresh Token 검증
            // 2. 새 Access Token 발급

            return Ok(new
            {
                message = "Refresh endpoint ready"
            });
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Logout request");

            // Authorization 헤더에서 토큰 추출
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ErrorResponse("INVALID_TOKEN", "토큰이 제공되지 않았습니다."));
            }

            var user = await _authService.GetUserByTokenAsync(token);
            if (user == null)
            {
                return Unauthorized(new ErrorResponse("INVALID_TOKEN", "유효하지 않은 토큰입니다."));
            }

            await _authService.LogoutAsync(user.Id);

            return Ok(new { message = "로그아웃 성공" });
        }

        // GET: api/auth/verify
        [HttpGet("verify")]
        public async Task<IActionResult> VerifyToken()
        {
            _logger.LogInformation("Token verification request");

            // Authorization 헤더에서 토큰 추출
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ErrorResponse("INVALID_TOKEN", "토큰이 제공되지 않았습니다."));
            }

            var user = await _authService.GetUserByTokenAsync(token);
            if (user == null)
            {
                return Unauthorized(new ErrorResponse("INVALID_TOKEN", "유효하지 않은 토큰입니다."));
            }

            return Ok(new UserInfoResponse(
                UserId: user.Id.ToString(),
                Username: user.Username,
                Email: user.Email,
                CreatedAt: user.CreatedAt
            ));
        }
    }
}
