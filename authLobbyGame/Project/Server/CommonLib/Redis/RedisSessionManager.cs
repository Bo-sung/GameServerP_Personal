using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis 기반 세션 저장소
    /// 여러 Lobby 서버 간 세션 공유 및 지속성 제공
    /// </summary>
    public class RedisSessionManager
    {
        private readonly RedisService _redisService;
        private readonly TimeSpan _defaultExpiry;
        private readonly string _keyPrefix;

        public RedisSessionManager(RedisService redisService, int expiryMinutes = 30, string keyPrefix = "session:")
        {
            _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
            _defaultExpiry = TimeSpan.FromMinutes(expiryMinutes);
            _keyPrefix = keyPrefix;
        }

        /// <summary>
        /// 세션 데이터 저장
        /// </summary>
        public async Task<bool> SaveSessionAsync(string sessionId, SessionData sessionData, TimeSpan? expiry = null)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected, session not saved");
                    return false;
                }

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                // Hash 구조로 저장
                var hashEntries = new HashEntry[]
                {
                    new HashEntry("userId", sessionData.UserId),
                    new HashEntry("userName", sessionData.UserName ?? ""),
                    new HashEntry("serverName", sessionData.ServerName ?? ""),
                    new HashEntry("ipAddress", sessionData.IpAddress ?? ""),
                    new HashEntry("loginTime", sessionData.LoginTime.ToString("o")),
                    new HashEntry("lastActivityTime", sessionData.LastActivityTime.ToString("o")),
                    new HashEntry("metadata", JsonSerializer.Serialize(sessionData.Metadata))
                };

                await db.HashSetAsync(key, hashEntries);
                await db.KeyExpireAsync(key, expiry ?? _defaultExpiry);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to save session {sessionId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 세션 데이터 조회
        /// </summary>
        public async Task<SessionData?> GetSessionAsync(string sessionId)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning("Redis not connected");
                    return null;
                }

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                var hashEntries = await db.HashGetAllAsync(key);
                if (hashEntries.Length == 0)
                    return null;

                var sessionData = new SessionData();
                foreach (var entry in hashEntries)
                {
                    switch (entry.Name.ToString())
                    {
                        case "userId":
                            sessionData.UserId = entry.Value.ToString();
                            break;
                        case "userName":
                            sessionData.UserName = entry.Value.ToString();
                            break;
                        case "serverName":
                            sessionData.ServerName = entry.Value.ToString();
                            break;
                        case "ipAddress":
                            sessionData.IpAddress = entry.Value.ToString();
                            break;
                        case "loginTime":
                            if (DateTime.TryParse(entry.Value.ToString(), out var loginTime))
                                sessionData.LoginTime = loginTime;
                            break;
                        case "lastActivityTime":
                            if (DateTime.TryParse(entry.Value.ToString(), out var lastActivityTime))
                                sessionData.LastActivityTime = lastActivityTime;
                            break;
                        case "metadata":
                            try
                            {
                                sessionData.Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.Value.ToString()) ?? new Dictionary<string, string>();
                            }
                            catch
                            {
                                sessionData.Metadata = new Dictionary<string, string>();
                            }
                            break;
                    }
                }

                return sessionData;
            }
            catch (Exception ex)
            {
                LogError($"Failed to get session {sessionId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 세션 마지막 활동 시간 갱신 (TTL도 갱신)
        /// </summary>
        public async Task<bool> UpdateLastActivityAsync(string sessionId, TimeSpan? expiry = null)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                await db.HashSetAsync(key, "lastActivityTime", DateTime.UtcNow.ToString("o"));
                await db.KeyExpireAsync(key, expiry ?? _defaultExpiry);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to update session activity {sessionId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 세션 삭제
        /// </summary>
        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                return await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete session {sessionId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 세션 존재 여부 확인
        /// </summary>
        public async Task<bool> SessionExistsAsync(string sessionId)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                return await db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                LogError($"Failed to check session existence {sessionId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 특정 사용자의 모든 세션 조회
        /// </summary>
        public async Task<List<string>> GetUserSessionsAsync(string userId)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return new List<string>();

                var db = _redisService.Database;
                var sessions = new List<string>();

                // 패턴 매칭으로 모든 세션 키 조회
                var server = _redisService.Database.Multiplexer.GetServer(_redisService.Database.Multiplexer.GetEndPoints()[0]);
                await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
                {
                    var storedUserId = await db.HashGetAsync(key, "userId");
                    if (storedUserId.HasValue && storedUserId.ToString() == userId)
                    {
                        sessions.Add(key.ToString().Substring(_keyPrefix.Length));
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                LogError($"Failed to get user sessions for {userId}: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 특정 사용자의 모든 세션 삭제 (중복 로그인 방지)
        /// </summary>
        public async Task<int> DeleteUserSessionsAsync(string userId)
        {
            try
            {
                var sessions = await GetUserSessionsAsync(userId);
                int deletedCount = 0;

                foreach (var sessionId in sessions)
                {
                    if (await DeleteSessionAsync(sessionId))
                        deletedCount++;
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete user sessions for {userId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 세션 메타데이터 업데이트
        /// </summary>
        public async Task<bool> UpdateMetadataAsync(string sessionId, Dictionary<string, string> metadata)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var key = GetKey(sessionId);
                var db = _redisService.Database;

                await db.HashSetAsync(key, "metadata", JsonSerializer.Serialize(metadata));
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to update metadata for {sessionId}: {ex.Message}");
                return false;
            }
        }

        private string GetKey(string sessionId) => $"{_keyPrefix}{sessionId}";

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisSession] WARNING: {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisSession] ERROR: {message}");
        }
    }

    /// <summary>
    /// 세션 데이터 모델
    /// </summary>
    public class SessionData
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
