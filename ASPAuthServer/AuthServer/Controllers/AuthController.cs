using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AuthServer.Models;
using AuthServer.Settings;
using AuthServer.Services.Auth;
using AuthServer.Data.Repositories;
using AuthServer.Services.Tokens;

namespace AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IAuthService _authService;
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IOptionsSnapshot<JwtSettings> jwtSettings,
            ILogger<AuthController> logger,
            ITokenService tokenService)
        {
            _authService = authService;
            _jwtSettings = jwtSettings;
            _logger = logger;
            _tokenService = tokenService;
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

            return Ok(
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

            // 게임 클라이언트용 로그인 (GameClient audience 사용)
            var (success, token, message, user) = await _authService.LoginAsync(
                request.Username,
                request.Password,
                request.DeviceId,
                "GameClient"
            );

            if (!success)
            {
                return Unauthorized(new ErrorResponse("LOGIN_FAILED", message ?? "로그인 실패"));
            }

            return Ok(new
            {
                token
            });
        }

        // POST: api/auth/exchange
        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangeToken([FromBody] ExchangeRequest request)
        {
            _logger.LogInformation("Exchange Requested {token} ", request.LoginToken);

            if (!await _tokenService.ValidateTokenAsync(request.LoginToken, request.DeviceId, ITokenService.TokenType.Login))
            {
                // 로그인 토큰이 유효하지 않음
                return Unauthorized(new ErrorResponse("INVALID_LOGIN_TOKEN", "유효하지 않은 로그인 토큰입니다."));
            }

            // 게임 클라이언트용 토큰 교환 (GameClient audience 사용)
            var result = await _tokenService.ExchangeTokensAsync(request.LoginToken, "GameClient");

            if (result.AccessToken == null || result.RefreshToken == null)
            {
                return Unauthorized(new ErrorResponse("TOKEN_EXCHANGE_FAILED", "토큰 교환 실패"));
            }

            // LoginToken을 사용됨으로 표시하여 재사용 방지 (1회용 토큰)
            await _tokenService.MarkLoginTokenAsUsedAsync(request.LoginToken);

            return Ok( new
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken
            });
        }

        // POST: api/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            // 리프레시 토큰 받아 검증 후 새 AT 발급.
            _logger.LogInformation("Token refresh request");

            bool result = await _tokenService.ValidateTokenAsync(request.RefreshToken, request.DeviceId, ITokenService.TokenType.Refresh);
            if(!result)
            {
                return Unauthorized(new ErrorResponse("INVALID_REFRESH_TOKEN", "유효하지 않은 리프레시 토큰입니다."));
            }

            // 게임 클라이언트용 토큰 갱신 (GameClient audience 사용)
            string? token = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken, "GameClient");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new ErrorResponse("TOKEN_REFRESH_FAILED", "토큰 갱신에 실패했습니다."));
            }

            return Ok(new
            {
                message = "Token Refreshed",
                token
            });
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            _logger.LogInformation("Logout request for DeviceId: {DeviceId}", request.DeviceId);

            // Authorization 헤더에서 Access Token 추출
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return BadRequest(new ErrorResponse("INVALID_TOKEN", "토큰이 제공되지 않았습니다."));
            }

            var accessToken = authHeader.Replace("Bearer ", "");

            // Access Token에서 userId 추출 (토큰 서비스가 처리)
            var (success, userId, errorMessage) = await _tokenService.ExtractUserIdFromTokenAsync(
                accessToken,
                ITokenService.TokenType.Access
            );

            if (!success)
            {
                return Unauthorized(new ErrorResponse("INVALID_TOKEN", errorMessage ?? "유효하지 않은 토큰입니다."));
            }

            // 모든 토큰 폐기 (Refresh Token 삭제)
            bool logoutSuccess = await _tokenService.RevokeAllUserTokensAsync(userId, request.DeviceId);

            if (!logoutSuccess)
            {
                _logger.LogWarning("Logout failed for UserId: {UserId}, DeviceId: {DeviceId}", userId, request.DeviceId);
                return StatusCode(500, new ErrorResponse("LOGOUT_FAILED", "로그아웃 처리 중 오류가 발생했습니다."));
            }

            _logger.LogInformation("Logout successful for UserId: {UserId}, DeviceId: {DeviceId}", userId, request.DeviceId);
            return Ok(new { message = "로그아웃 성공" });
        }
    }
}
