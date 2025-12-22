using AuthServer.Data.Repositories;
using AuthServer.Data;
using AuthServer.Models;
using AuthServer.Settings;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AuthServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRedisConnectionFactory _redisFactory;
        private readonly SecuritySettings _securitySettings;

        public AuthService(
            IUserRepository userRepository,
            IRedisConnectionFactory redisFactory,
            IOptions<SecuritySettings> securitySettings)
        {
            _userRepository = userRepository;
            _redisFactory = redisFactory;
            _securitySettings = securitySettings.Value;
        }

        public async Task<(bool Success, string? Token, string? Message, User? User)> LoginAsync(string username, string password)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
                return (false, null, "사용자를 찾을 수 없습니다.", null);

            // 계정 잠금 확인
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var remainingTime = (user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                return (false, null, $"계정이 잠겼습니다. {Math.Ceiling(remainingTime)}분 후 다시 시도하세요.", null);
            }

            // 비밀번호 검증
            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash)
            {
                // 로그인 실패 카운트 증가
                user.LoginAttempts++;

                if (user.LoginAttempts >= _securitySettings.MaxLoginAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(_securitySettings.LockoutDurationMinutes);
                    await _userRepository.UpdateAsync(user);
                    return (false, null, $"로그인 시도 횟수 초과. 계정이 {_securitySettings.LockoutDurationMinutes}분간 잠겼습니다.", null);
                }

                await _userRepository.UpdateAsync(user);
                return (false, null, $"비밀번호가 일치하지 않습니다. (남은 시도: {_securitySettings.MaxLoginAttempts - user.LoginAttempts}회)", null);
            }

            // 로그인 성공 - 실패 카운트 초기화
            user.LoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            // 토큰 생성 (임시로 간단한 토큰, 실제로는 JWT 사용)
            var token = GenerateToken(user);

            // Redis에 세션 저장
            var redis = _redisFactory.GetDatabase();
            await redis.StringSetAsync($"session:{user.Id}", token, TimeSpan.FromHours(1));
            await redis.StringSetAsync($"token:{token}", user.Id.ToString(), TimeSpan.FromHours(1));

            return (true, token, "로그인 성공", user);
        }

        public async Task<(bool Success, int? UserId, string? Message)> RegisterAsync(string username, string email, string password)
        {
            // 중복 확인
            if (await _userRepository.ExistsAsync(username, email))
                return (false, null, "이미 존재하는 사용자명 또는 이메일입니다.");

            // 비밀번호 검증 (간단한 예시)
            if (password.Length < 6)
                return (false, null, "비밀번호는 최소 6자 이상이어야 합니다.");

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                LoginAttempts = 0
            };

            var userId = await _userRepository.CreateAsync(user);
            return (true, userId, "회원가입 성공");
        }

        public async Task<bool> LogoutAsync(int userId)
        {
            var redis = _redisFactory.GetDatabase();

            // 세션에서 토큰 가져오기
            var token = await redis.StringGetAsync($"session:{userId}");

            // 토큰과 세션 모두 삭제
            var tasks = new List<Task<bool>>
            {
                redis.KeyDeleteAsync($"session:{userId}"),
                redis.KeyDeleteAsync($"token:{token}")
            };

            await Task.WhenAll(tasks);
            return true;
        }

        public async Task<User?> GetUserByTokenAsync(string token)
        {
            var redis = _redisFactory.GetDatabase();
            var userIdStr = await redis.StringGetAsync($"token:{token}");

            if (userIdStr.IsNullOrEmpty)
                return null;

            if (int.TryParse(userIdStr, out int userId))
            {
                return await _userRepository.GetByIdAsync(userId);
            }

            return null;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            var redis = _redisFactory.GetDatabase();
            return await redis.KeyExistsAsync($"token:{token}");
        }

        private string HashPassword(string password)
        {
            // SHA256 해싱 (실제로는 BCrypt나 PBKDF2 사용 권장)
            using var sha256 = SHA256.Create();
            var iterations = _securitySettings.PasswordHashIterations;

            var bytes = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < iterations; i++)
            {
                bytes = sha256.ComputeHash(bytes);
            }

            return Convert.ToBase64String(bytes);
        }

        private string GenerateToken(User user)
        {
            // 임시 토큰 생성 (실제로는 JWT 사용)
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return $"token_{user.Id}_{Convert.ToBase64String(randomBytes)}";
        }
    }
}
