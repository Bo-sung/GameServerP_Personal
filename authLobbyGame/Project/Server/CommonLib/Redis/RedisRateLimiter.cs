using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis 기반 Rate Limiting 서비스
    /// API 호출 횟수 제한, DDoS 방어, 스팸 방지
    /// </summary>
    public class RedisRateLimiter
    {
        private readonly RedisService _redisService;
        private readonly string _keyPrefix;

        public RedisRateLimiter(RedisService redisService, string keyPrefix = "ratelimit:")
        {
            _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
            _keyPrefix = keyPrefix;
        }

        /// <summary>
        /// Fixed Window 알고리즘: 고정된 시간 윈도우에서 요청 횟수 제한
        /// </summary>
        public async Task<RateLimitResult> CheckFixedWindowAsync(string identifier, int maxRequests, TimeSpan window)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected, rate limiting bypassed");
                    return new RateLimitResult { IsAllowed = true, Remaining = maxRequests };
                }

                var key = GetKey($"fixed:{identifier}");
                var db = _redisService.Database;

                // 현재 카운트 증가
                var currentCount = await db.StringIncrementAsync(key);

                // 첫 요청이면 TTL 설정
                if (currentCount == 1)
                {
                    await db.KeyExpireAsync(key, window);
                }

                var isAllowed = currentCount <= maxRequests;
                var remaining = Math.Max(0, maxRequests - (int)currentCount);

                if (!isAllowed)
                {
                    LogWarning($"Rate limit exceeded for '{identifier}': {currentCount}/{maxRequests}");
                }

                return new RateLimitResult
                {
                    IsAllowed = isAllowed,
                    Remaining = remaining,
                    TotalRequests = maxRequests,
                    CurrentCount = (int)currentCount
                };
            }
            catch (Exception ex)
            {
                LogError($"Failed to check rate limit for '{identifier}': {ex.Message}");
                return new RateLimitResult { IsAllowed = true, Remaining = maxRequests };
            }
        }

        /// <summary>
        /// Sliding Window 알고리즘: 슬라이딩 시간 윈도우에서 요청 횟수 제한
        /// </summary>
        public async Task<RateLimitResult> CheckSlidingWindowAsync(string identifier, int maxRequests, TimeSpan window)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected, rate limiting bypassed");
                    return new RateLimitResult { IsAllowed = true, Remaining = maxRequests };
                }

                var key = GetKey($"sliding:{identifier}");
                var db = _redisService.Database;
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var windowStart = now - (long)window.TotalMilliseconds;

                // 1. 오래된 항목 제거
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

                // 2. 현재 윈도우의 요청 수 조회
                var currentCount = await db.SortedSetLengthAsync(key);

                var isAllowed = currentCount < maxRequests;

                if (isAllowed)
                {
                    // 3. 새 요청 추가
                    await db.SortedSetAddAsync(key, now.ToString(), now);
                    await db.KeyExpireAsync(key, window);
                    currentCount++;
                }

                var remaining = Math.Max(0, maxRequests - (int)currentCount);

                if (!isAllowed)
                {
                    LogWarning($"Sliding window rate limit exceeded for '{identifier}': {currentCount}/{maxRequests}");
                }

                return new RateLimitResult
                {
                    IsAllowed = isAllowed,
                    Remaining = remaining,
                    TotalRequests = maxRequests,
                    CurrentCount = (int)currentCount
                };
            }
            catch (Exception ex)
            {
                LogError($"Failed to check sliding window rate limit for '{identifier}': {ex.Message}");
                return new RateLimitResult { IsAllowed = true, Remaining = maxRequests };
            }
        }

        /// <summary>
        /// Token Bucket 알고리즘: 토큰 버킷 기반 Rate Limiting
        /// </summary>
        public async Task<RateLimitResult> CheckTokenBucketAsync(string identifier, int bucketSize, int refillRate, TimeSpan refillInterval)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected, rate limiting bypassed");
                    return new RateLimitResult { IsAllowed = true, Remaining = bucketSize };
                }

                var key = GetKey($"bucket:{identifier}");
                var db = _redisService.Database;

                var hashEntries = await db.HashGetAllAsync(key);

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tokens = bucketSize;
                var lastRefill = now;

                // 기존 데이터 로드
                if (hashEntries.Length > 0)
                {
                    foreach (var entry in hashEntries)
                    {
                        if (entry.Name == "tokens" && long.TryParse(entry.Value, out var t))
                            tokens = (int)t;
                        else if (entry.Name == "lastRefill" && long.TryParse(entry.Value, out var lr))
                            lastRefill = lr;
                    }

                    // 경과 시간에 따라 토큰 리필
                    var elapsedIntervals = (now - lastRefill) / (long)refillInterval.TotalSeconds;
                    if (elapsedIntervals > 0)
                    {
                        tokens = Math.Min(bucketSize, tokens + (int)(elapsedIntervals * refillRate));
                        lastRefill = now;
                    }
                }

                var isAllowed = tokens > 0;

                if (isAllowed)
                {
                    tokens--;
                }

                // 상태 저장
                await db.HashSetAsync(key, new HashEntry[]
                {
                    new HashEntry("tokens", tokens),
                    new HashEntry("lastRefill", lastRefill)
                });
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(3600)); // 1시간 TTL

                if (!isAllowed)
                {
                    LogWarning($"Token bucket depleted for '{identifier}'");
                }

                return new RateLimitResult
                {
                    IsAllowed = isAllowed,
                    Remaining = tokens,
                    TotalRequests = bucketSize,
                    CurrentCount = bucketSize - tokens
                };
            }
            catch (Exception ex)
            {
                LogError($"Failed to check token bucket for '{identifier}': {ex.Message}");
                return new RateLimitResult { IsAllowed = true, Remaining = bucketSize };
            }
        }

        /// <summary>
        /// IP 기반 Rate Limiting
        /// </summary>
        public async Task<RateLimitResult> CheckIpRateLimitAsync(string ipAddress, int maxRequests, TimeSpan window)
        {
            return await CheckFixedWindowAsync($"ip:{ipAddress}", maxRequests, window);
        }

        /// <summary>
        /// 사용자 기반 Rate Limiting
        /// </summary>
        public async Task<RateLimitResult> CheckUserRateLimitAsync(string userId, int maxRequests, TimeSpan window)
        {
            return await CheckFixedWindowAsync($"user:{userId}", maxRequests, window);
        }

        /// <summary>
        /// API 엔드포인트별 Rate Limiting
        /// </summary>
        public async Task<RateLimitResult> CheckApiRateLimitAsync(string userId, string endpoint, int maxRequests, TimeSpan window)
        {
            return await CheckFixedWindowAsync($"api:{userId}:{endpoint}", maxRequests, window);
        }

        /// <summary>
        /// 로그인 시도 Rate Limiting (브루트포스 방어)
        /// </summary>
        public async Task<RateLimitResult> CheckLoginAttemptAsync(string identifier, int maxAttempts, TimeSpan lockoutDuration)
        {
            return await CheckFixedWindowAsync($"login:{identifier}", maxAttempts, lockoutDuration);
        }

        /// <summary>
        /// Rate Limit 초기화 (관리자용)
        /// </summary>
        public async Task<bool> ResetRateLimitAsync(string identifier)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var db = _redisService.Database;
                var patterns = new[] { $"fixed:{identifier}", $"sliding:{identifier}", $"bucket:{identifier}" };

                foreach (var pattern in patterns)
                {
                    var key = GetKey(pattern);
                    await db.KeyDeleteAsync(key);
                }

                LogInfo($"Reset rate limit for '{identifier}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to reset rate limit for '{identifier}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rate Limit 상태 조회
        /// </summary>
        public async Task<RateLimitStatus?> GetRateLimitStatusAsync(string identifier)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return null;

                var key = GetKey($"fixed:{identifier}");
                var db = _redisService.Database;

                var count = await db.StringGetAsync(key);
                var ttl = await db.KeyTimeToLiveAsync(key);

                if (!count.HasValue)
                    return null;

                return new RateLimitStatus
                {
                    Identifier = identifier,
                    CurrentCount = (int)count,
                    ResetIn = ttl ?? TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                LogError($"Failed to get rate limit status for '{identifier}': {ex.Message}");
                return null;
            }
        }

        private string GetKey(string key) => $"{_keyPrefix}{key}";

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RateLimit] {message}");
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RateLimit] WARNING: {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RateLimit] ERROR: {message}");
        }
    }

    /// <summary>
    /// Rate Limit 체크 결과
    /// </summary>
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int Remaining { get; set; }
        public int TotalRequests { get; set; }
        public int CurrentCount { get; set; }
    }

    /// <summary>
    /// Rate Limit 상태
    /// </summary>
    public class RateLimitStatus
    {
        public string Identifier { get; set; } = string.Empty;
        public int CurrentCount { get; set; }
        public TimeSpan ResetIn { get; set; }
    }

    /// <summary>
    /// 일반적인 Rate Limit 프로필
    /// </summary>
    public static class RateLimitProfiles
    {
        // API 엔드포인트 제한
        public static readonly (int maxRequests, TimeSpan window) ApiStandard = (100, TimeSpan.FromMinutes(1));
        public static readonly (int maxRequests, TimeSpan window) ApiStrict = (10, TimeSpan.FromMinutes(1));

        // 로그인 시도 제한
        public static readonly (int maxAttempts, TimeSpan lockout) LoginAttempt = (5, TimeSpan.FromMinutes(15));

        // 채팅 메시지 제한
        public static readonly (int maxMessages, TimeSpan window) ChatMessage = (10, TimeSpan.FromSeconds(10));

        // 회원가입 제한
        public static readonly (int maxRequests, TimeSpan window) Registration = (3, TimeSpan.FromHours(1));
    }
}
