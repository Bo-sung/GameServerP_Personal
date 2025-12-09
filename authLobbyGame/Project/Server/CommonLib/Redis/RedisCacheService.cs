using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis 기반 캐싱 서비스
    /// DB 조회 결과, 자주 사용되는 데이터, 계산 결과 등을 캐싱
    /// </summary>
    public class RedisCacheService
    {
        private readonly RedisService _redisService;
        private readonly TimeSpan _defaultExpiry;
        private readonly string _keyPrefix;

        public RedisCacheService(RedisService redisService, int defaultExpiryMinutes = 10, string keyPrefix = "cache:")
        {
            _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
            _defaultExpiry = TimeSpan.FromMinutes(defaultExpiryMinutes);
            _keyPrefix = keyPrefix;
        }

        /// <summary>
        /// 캐시에 문자열 저장
        /// </summary>
        public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected, cache not set");
                    return false;
                }

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.StringSetAsync(fullKey, value, expiry ?? _defaultExpiry);
            }
            catch (Exception ex)
            {
                LogError($"Failed to set cache '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 캐시에 객체 저장 (JSON 직렬화)
        /// </summary>
        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                return await SetAsync(key, json, expiry);
            }
            catch (Exception ex)
            {
                LogError($"Failed to serialize and cache '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 캐시에서 문자열 조회
        /// </summary>
        public async Task<string?> GetAsync(string key)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected");
                    return null;
                }

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                var value = await db.StringGetAsync(fullKey);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                LogError($"Failed to get cache '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 캐시에서 객체 조회 (JSON 역직렬화)
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var json = await GetAsync(key);
                if (string.IsNullOrEmpty(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to deserialize cache '{key}': {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Cache-Aside 패턴: 캐시 조회 후 없으면 데이터 생성 및 캐싱
        /// </summary>
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            try
            {
                // 1. 캐시 조회
                var cached = await GetAsync<T>(key);
                if (cached != null)
                {
                    LogInfo($"Cache HIT: {key}");
                    return cached;
                }

                // 2. 캐시 미스 - 데이터 생성
                LogInfo($"Cache MISS: {key}");
                var value = await factory();

                if (value != null)
                {
                    // 3. 캐시에 저장
                    await SetAsync(key, value, expiry);
                }

                return value;
            }
            catch (Exception ex)
            {
                LogError($"Failed in GetOrCreateAsync for '{key}': {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 캐시 삭제
        /// </summary>
        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.KeyDeleteAsync(fullKey);
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete cache '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 패턴 매칭으로 여러 키 삭제 (예: "user:*")
        /// </summary>
        public async Task<int> DeleteByPatternAsync(string pattern)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return 0;

                var db = _redisService.Database;
                var server = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints()[0]);
                var fullPattern = GetKey(pattern);

                int deletedCount = 0;
                await foreach (var key in server.KeysAsync(pattern: fullPattern))
                {
                    if (await db.KeyDeleteAsync(key))
                        deletedCount++;
                }

                LogInfo($"Deleted {deletedCount} keys matching pattern '{pattern}'");
                return deletedCount;
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete by pattern '{pattern}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 캐시 존재 여부 확인
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.KeyExistsAsync(fullKey);
            }
            catch (Exception ex)
            {
                LogError($"Failed to check existence of '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 캐시 TTL 연장
        /// </summary>
        public async Task<bool> RefreshAsync(string key, TimeSpan? expiry = null)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.KeyExpireAsync(fullKey, expiry ?? _defaultExpiry);
            }
            catch (Exception ex)
            {
                LogError($"Failed to refresh TTL for '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 증가 연산 (카운터용)
        /// </summary>
        public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return 0;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                var result = await db.StringIncrementAsync(fullKey, value);

                if (expiry.HasValue)
                {
                    await db.KeyExpireAsync(fullKey, expiry.Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                LogError($"Failed to increment '{key}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 감소 연산 (카운터용)
        /// </summary>
        public async Task<long> DecrementAsync(string key, long value = 1)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return 0;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.StringDecrementAsync(fullKey, value);
            }
            catch (Exception ex)
            {
                LogError($"Failed to decrement '{key}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Hash 필드 설정
        /// </summary>
        public async Task<bool> HashSetAsync(string key, string field, string value)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.HashSetAsync(fullKey, field, value);
            }
            catch (Exception ex)
            {
                LogError($"Failed to hash set '{key}.{field}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hash 필드 조회
        /// </summary>
        public async Task<string?> HashGetAsync(string key, string field)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return null;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                var value = await db.HashGetAsync(fullKey, field);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                LogError($"Failed to hash get '{key}.{field}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Hash 전체 조회
        /// </summary>
        public async Task<HashEntry[]?> HashGetAllAsync(string key)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return null;

                var fullKey = GetKey(key);
                var db = _redisService.Database;

                return await db.HashGetAllAsync(fullKey);
            }
            catch (Exception ex)
            {
                LogError($"Failed to hash get all '{key}': {ex.Message}");
                return null;
            }
        }

        private string GetKey(string key) => $"{_keyPrefix}{key}";

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisCache] {message}");
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisCache] WARNING: {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisCache] ERROR: {message}");
        }
    }

    /// <summary>
    /// 일반적인 캐시 키 상수
    /// </summary>
    public static class CacheKeys
    {
        public const string UserProfile = "user:profile:{0}";
        public const string ServerList = "server:list:{0}";
        public const string RoomList = "room:list";
        public const string UserStats = "user:stats:{0}";
        public const string Leaderboard = "leaderboard:{0}";

        public static string GetUserProfileKey(string userId) => string.Format(UserProfile, userId);
        public static string GetServerListKey(string serverType) => string.Format(ServerList, serverType);
        public static string GetUserStatsKey(string userId) => string.Format(UserStats, userId);
        public static string GetLeaderboardKey(string category) => string.Format(Leaderboard, category);
    }
}
