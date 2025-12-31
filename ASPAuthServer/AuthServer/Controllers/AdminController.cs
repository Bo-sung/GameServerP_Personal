using Microsoft.AspNetCore.Authorization;
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
    public class AdminController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository;
        private readonly IAdminRepository _adminRepository;
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettings;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAuthService authService,
            IUserRepository userRepository,
            IAdminRepository adminRepository,
            IOptionsSnapshot<JwtSettings> jwtSettings,
            ILogger<AdminController> logger,
            ITokenService tokenService)
        {
            _authService = authService;
            _userRepository = userRepository;
            _adminRepository = adminRepository;
            _jwtSettings = jwtSettings;
            _logger = logger;
            _tokenService = tokenService;
        }

        // POST: api/admin/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Admin login request for username: {Username}", request.Username);

            // Admin 테이블에서 관리자 계정 확인
            var admin = await _adminRepository.GetByUsernameAsync(request.Username);
            if (admin == null)
            {
                _logger.LogWarning("Admin not found: {Username}", request.Username);
                return Unauthorized(new ErrorResponse("ADMIN_LOGIN_FAILED", "관리자 계정이 존재하지 않습니다."));
            }

            // 계정 잠금 확인
            if (admin.LockedUntil.HasValue && admin.LockedUntil.Value > DateTime.UtcNow)
            {
                _logger.LogWarning("Admin account locked: {Username}", request.Username);
                return Unauthorized(new ErrorResponse("ADMIN_LOCKED", $"관리자 계정이 잠겨있습니다. 잠금 해제 시간: {admin.LockedUntil.Value}"));
            }

            // 계정 활성 상태 확인
            if (!admin.IsActive)
            {
                _logger.LogWarning("Admin account inactive: {Username}", request.Username);
                return Unauthorized(new ErrorResponse("ADMIN_INACTIVE", "관리자 계정이 비활성화되었습니다."));
            }

            // 비밀번호 검증 (AuthService의 로그인 로직 사용)
            var (success, token, message, user) = await _authService.LoginAsync(
                request.Username,
                request.Password,
                request.DeviceId ?? "admin-default-device",
                "AdminPanel"
            );

            if (!success)
            {
                // 로그인 실패 시 시도 횟수 증가
                admin.LoginAttempts++;
                if (admin.LoginAttempts >= 5)
                {
                    admin.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                    _logger.LogWarning("Admin account locked due to failed attempts: {Username}", request.Username);
                }
                await _adminRepository.UpdateAsync(admin);

                return Unauthorized(new ErrorResponse("ADMIN_LOGIN_FAILED", message ?? "관리자 로그인 실패"));
            }

            // 관리자 Role 검증
            if (string.IsNullOrEmpty(admin.Role))
            {
                _logger.LogWarning("Admin has no role: {Username}", request.Username);
                return StatusCode(403, new ErrorResponse("ADMIN_NO_ROLE", "관리자 권한이 설정되지 않았습니다. 시스템 관리자에게 문의하세요."));
            }

            // 로그인 성공 - 시도 횟수 초기화 및 마지막 로그인 시간 업데이트
            admin.LoginAttempts = 0;
            admin.LockedUntil = null;
            admin.LastLoginAt = DateTime.UtcNow;
            await _adminRepository.UpdateAsync(admin);

            _logger.LogInformation("Admin login successful: {Username}, Role: {Role}", request.Username, admin.Role);

            return Ok(new
            {
                token,
                expiresIn = _jwtSettings.Get("Admin").LoginTokenExpirationMinutes,
                role = admin.Role
            });
        }

        // POST: api/admin/exchange
        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangeToken([FromBody] ExchangeRequest request)
        {
            _logger.LogInformation("Admin exchange requested");

            if (!await _tokenService.ValidateTokenAsync(request.LoginToken, request.DeviceId, ITokenService.TokenType.Login))
            {
                return Unauthorized(new ErrorResponse("INVALID_ADMIN_TOKEN", "유효하지 않은 관리자 토큰입니다."));
            }

            // 관리자용 토큰 교환 (AdminPanel audience 사용)
            var result = await _tokenService.ExchangeTokensAsync(request.LoginToken, "AdminPanel");

            if (result.AccessToken == null || result.RefreshToken == null)
            {
                return Unauthorized(new ErrorResponse("TOKEN_EXCHANGE_FAILED", "관리자 토큰 교환 실패"));
            }

            await _tokenService.MarkLoginTokenAsUsedAsync(request.LoginToken);

            return Ok(new
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken
            });
        }

        // POST: api/admin/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            _logger.LogInformation("Admin token refresh request");

            bool result = await _tokenService.ValidateTokenAsync(request.RefreshToken, request.DeviceId, ITokenService.TokenType.Refresh);
            if (!result)
            {
                return Unauthorized(new ErrorResponse("INVALID_REFRESH_TOKEN", "유효하지 않은 관리자 리프레시 토큰입니다."));
            }

            // 관리자용 토큰 갱신 (AdminPanel audience 사용)
            string? token = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken, "AdminPanel");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new ErrorResponse("TOKEN_REFRESH_FAILED", "관리자 토큰 갱신에 실패했습니다."));
            }

            return Ok(new
            {
                message = "Admin Token Refreshed",
                token
            });
        }

        // GET: api/admin/users
        [Authorize]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            _logger.LogInformation("Get all users request - Page: {Page}, PageSize: {PageSize}, Search: {Search}",
                page, pageSize, search);

            // TODO: UserRepository에 페이징 및 검색 기능 추가 필요

            // 페이지 크기 제한
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 10;
            if (page < 1) page = 1;

            return Ok(new
            {
                message = "Get users endpoint - Implementation pending",
                page,
                pageSize,
                search,
                isActive,
                note = "UserRepository에 GetAllUsersAsync(page, pageSize, search, isActive) 메서드 구현 필요"
            });
        }

        // GET: api/admin/users/{userId}
        [Authorize]
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUser(int userId)
        {
            _logger.LogInformation("Get user request for userId: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                isActive = user.IsActive,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
                loginAttempts = user.LoginAttempts,
                lockedUntil = user.LockedUntil
            });
        }

        // PATCH: api/admin/users/{userId}/lock
        [Authorize]
        [HttpPatch("users/{userId}/lock")]
        public async Task<IActionResult> LockUser(int userId, [FromBody] LockUserRequest request)
        {
            _logger.LogInformation("Lock user request for userId: {UserId}, Lock: {Lock}", userId, request.Lock);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            if (request.Lock)
            {
                int duration = request.DurationMinutes ?? 30;
                user.LockedUntil = DateTime.UtcNow.AddMinutes(duration);
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("User {UserId} locked until {LockedUntil}", userId, user.LockedUntil);

                return Ok(new
                {
                    message = "사용자 계정이 잠겼습니다.",
                    userId,
                    lockedUntil = user.LockedUntil
                });
            }
            else
            {
                user.LockedUntil = null;
                user.LoginAttempts = 0;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("User {UserId} unlocked", userId);

                return Ok(new
                {
                    message = "사용자 계정 잠금이 해제되었습니다.",
                    userId
                });
            }
        }

        // POST: api/admin/users/{userId}/reset-password
        [Authorize]
        [HttpPost("users/{userId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int userId, [FromBody] ResetPasswordRequest request)
        {
            _logger.LogInformation("Reset password request for userId: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new ErrorResponse("INVALID_PASSWORD", "비밀번호는 최소 6자 이상이어야 합니다."));
            }

            // TODO: AuthService에 ChangePassword 메서드 추가 또는 직접 해싱
            // 현재는 구조상 AuthService의 HashPassword가 private이므로 접근 불가
            // 추후 IPasswordHasher 인터페이스 분리 권장

            return Ok(new
            {
                message = "비밀번호 초기화 기능 - 구현 대기",
                userId,
                note = "AuthService에 공개 HashPassword 메서드 또는 IPasswordHasher 인터페이스 필요"
            });
        }

        // DELETE: api/admin/users/{userId}/sessions
        [Authorize]
        [HttpDelete("users/{userId}/sessions")]
        public async Task<IActionResult> TerminateSessions(int userId, [FromQuery] string? deviceId = null)
        {
            _logger.LogInformation("Terminate sessions request for userId: {UserId}, DeviceId: {DeviceId}",
                userId, deviceId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            if (!string.IsNullOrEmpty(deviceId))
            {
                // 특정 디바이스만 종료
                bool success = await _tokenService.RevokeAllUserTokensAsync(userId, deviceId);
                if (success)
                {
                    return Ok(new
                    {
                        message = "사용자의 세션이 종료되었습니다.",
                        userId,
                        deviceId
                    });
                }
                else
                {
                    return StatusCode(500, new ErrorResponse("SESSION_TERMINATE_FAILED", "세션 종료에 실패했습니다."));
                }
            }
            else
            {
                // TODO: 모든 디바이스 세션 종료 기능 구현 필요
                // 현재 TokenService는 deviceId 단위로만 작동
                // Redis에서 "refresh:userId:*" 패턴으로 모든 디바이스 검색 후 삭제 필요

                return Ok(new
                {
                    message = "모든 세션 종료 기능 - 구현 대기",
                    userId,
                    note = "TokenService에 RevokeAllUserTokensAllDevices(userId) 메서드 추가 필요"
                });
            }
        }

        // DELETE: api/admin/users/{userId}
        [Authorize]
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            _logger.LogInformation("Delete user request for userId: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new ErrorResponse("USER_NOT_FOUND", "사용자를 찾을 수 없습니다."));
            }

            // TODO: UserRepository에 DeleteAsync 메서드 추가 필요
            // 또한 사용자 삭제 시 관련 토큰도 모두 삭제해야 함

            return Ok(new
            {
                message = "사용자 삭제 기능 - 구현 대기",
                userId,
                note = "UserRepository에 DeleteAsync 메서드 추가 필요, Redis 토큰도 함께 삭제"
            });
        }

        // GET: api/admin/statistics
        [Authorize]
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            _logger.LogInformation("Get statistics request");

            try
            {
                var totalUsers = await _userRepository.GetTotalUsersCountAsync();
                var activeUsers = await _userRepository.GetActiveUsersCountAsync();
                var lockedUsers = await _userRepository.GetLockedUsersCountAsync();
                var todayRegistrations = await _userRepository.GetTodayRegistrationsCountAsync();
                var todayLogins = await _userRepository.GetTodayLoginsCountAsync();

                // TODO: OnlineUsers는 Redis에서 현재 활성 세션 수를 가져와야 함
                // RefreshToken 키 개수를 세거나, 별도의 온라인 사용자 추적 메커니즘 필요
                var onlineUsers = 0;

                return Ok(new
                {
                    totalUsers,
                    activeUsers,
                    lockedUsers,
                    onlineUsers,
                    todayRegistrations,
                    todayLogins
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "통계 조회 중 오류 발생");
                return StatusCode(500, new ErrorResponse("STATISTICS_ERROR", "통계 조회 중 오류가 발생했습니다."));
            }
        }
    }
}
