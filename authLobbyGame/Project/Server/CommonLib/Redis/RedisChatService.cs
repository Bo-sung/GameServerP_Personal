using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis 기반 채팅 서비스
    /// Pub/Sub를 활용한 실시간 채팅 및 채널 관리
    /// </summary>
    public class RedisChatService : IDisposable
    {
        private readonly RedisPubSubService _pubSubService;
        private readonly RedisRateLimiter _rateLimiter;
        private readonly RedisCacheService _cacheService;
        private readonly ConcurrentDictionary<string, HashSet<string>> _channelUsers; // channel -> userIds
        private readonly ConcurrentDictionary<string, Action<ChatMessageEvent>> _messageHandlers;
        private bool _isDisposed;

        public RedisChatService(
            RedisPubSubService pubSubService,
            RedisRateLimiter rateLimiter,
            RedisCacheService cacheService)
        {
            _pubSubService = pubSubService ?? throw new ArgumentNullException(nameof(pubSubService));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _channelUsers = new ConcurrentDictionary<string, HashSet<string>>();
            _messageHandlers = new ConcurrentDictionary<string, Action<ChatMessageEvent>>();
        }

        /// <summary>
        /// 채팅 채널 참가
        /// </summary>
        public async Task<bool> JoinChannelAsync(string channelId, string userId, string userName, Action<ChatMessageEvent> messageHandler)
        {
            try
            {
                // 채널의 사용자 목록에 추가
                var users = _channelUsers.GetOrAdd(channelId, _ => new HashSet<string>());
                lock (users)
                {
                    users.Add(userId);
                }

                // 메시지 핸들러 등록
                _messageHandlers[userId] = messageHandler;

                // Redis Pub/Sub 채널 구독
                var channel = PubSubChannels.GetChatChannel(channelId);
                await _pubSubService.SubscribeAsync(channel, (PubSubMessage message) =>
                {
                    try
                    {
                        var chatEvent = JsonSerializer.Deserialize<ChatMessageEvent>(message.Payload);
                        if (chatEvent != null && _messageHandlers.TryGetValue(userId, out var handler))
                        {
                            handler(chatEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to handle chat message: {ex.Message}");
                    }
                });

                // 채널 참가 알림 브로드캐스트
                await BroadcastSystemMessageAsync(channelId, $"{userName} has joined the channel");

                // 채널 사용자 수 캐싱
                await UpdateChannelUserCountAsync(channelId);

                LogInfo($"User '{userName}' ({userId}) joined channel '{channelId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to join channel '{channelId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채팅 채널 퇴장
        /// </summary>
        public async Task<bool> LeaveChannelAsync(string channelId, string userId, string userName)
        {
            try
            {
                // 채널의 사용자 목록에서 제거
                if (_channelUsers.TryGetValue(channelId, out var users))
                {
                    lock (users)
                    {
                        users.Remove(userId);
                    }

                    // 채널에 아무도 없으면 구독 해제
                    if (users.Count == 0)
                    {
                        var channel = PubSubChannels.GetChatChannel(channelId);
                        await _pubSubService.UnsubscribeAsync(channel);
                        _channelUsers.TryRemove(channelId, out _);
                    }
                }

                // 메시지 핸들러 제거
                _messageHandlers.TryRemove(userId, out _);

                // 채널 퇴장 알림 브로드캐스트
                await BroadcastSystemMessageAsync(channelId, $"{userName} has left the channel");

                // 채널 사용자 수 캐싱
                await UpdateChannelUserCountAsync(channelId);

                LogInfo($"User '{userName}' ({userId}) left channel '{channelId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to leave channel '{channelId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채팅 메시지 전송
        /// </summary>
        public async Task<ChatSendResult> SendMessageAsync(string channelId, string userId, string userName, string message)
        {
            try
            {
                // Rate Limiting 체크
                var rateLimitResult = await _rateLimiter.CheckUserRateLimitAsync(
                    $"chat:{userId}",
                    RateLimitProfiles.ChatMessage.maxMessages,
                    RateLimitProfiles.ChatMessage.window);

                if (!rateLimitResult.IsAllowed)
                {
                    LogWarning($"Rate limit exceeded for user '{userId}' in chat");
                    return new ChatSendResult
                    {
                        Success = false,
                        ErrorMessage = "Too many messages. Please slow down.",
                        RateLimited = true
                    };
                }

                // 메시지 필터링 (욕설, 스팸 등)
                var filteredMessage = FilterMessage(message);

                // 채팅 메시지 이벤트 생성
                var chatEvent = new ChatMessageEvent
                {
                    ChannelId = channelId,
                    UserId = userId,
                    UserName = userName,
                    Message = filteredMessage,
                    Timestamp = DateTime.UtcNow,
                    MessageType = ChatMessageType.User
                };

                // Redis Pub/Sub로 브로드캐스트
                var channel = PubSubChannels.GetChatChannel(channelId);
                var success = await _pubSubService.PublishAsync(channel, new PubSubMessage
                {
                    MessageType = "Chat",
                    SenderId = userId,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(chatEvent)
                });

                if (success)
                {
                    // 채팅 히스토리 저장 (최근 100개만)
                    await SaveChatHistoryAsync(channelId, chatEvent);
                    LogInfo($"Chat message sent in '{channelId}' by '{userName}': {filteredMessage}");
                }

                return new ChatSendResult
                {
                    Success = success,
                    ErrorMessage = success ? null : "Failed to send message"
                };
            }
            catch (Exception ex)
            {
                LogError($"Failed to send message in '{channelId}': {ex.Message}");
                return new ChatSendResult
                {
                    Success = false,
                    ErrorMessage = "Internal error"
                };
            }
        }

        /// <summary>
        /// 시스템 메시지 브로드캐스트
        /// </summary>
        private async Task<bool> BroadcastSystemMessageAsync(string channelId, string message)
        {
            try
            {
                var chatEvent = new ChatMessageEvent
                {
                    ChannelId = channelId,
                    UserId = "SYSTEM",
                    UserName = "System",
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    MessageType = ChatMessageType.System
                };

                var channel = PubSubChannels.GetChatChannel(channelId);
                return await _pubSubService.PublishAsync(channel, new PubSubMessage
                {
                    MessageType = "Chat",
                    SenderId = "SYSTEM",
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(chatEvent)
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to broadcast system message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채팅 히스토리 저장
        /// </summary>
        private async Task SaveChatHistoryAsync(string channelId, ChatMessageEvent chatEvent)
        {
            try
            {
                var key = $"chat:history:{channelId}";
                var db = _cacheService;

                // Sorted Set을 사용해 시간순 저장 (직접 Redis 접근 필요)
                // 여기서는 간단히 캐시로 구현
                var historyKey = $"history:{channelId}";
                var history = await _cacheService.GetAsync<List<ChatMessageEvent>>(historyKey) ?? new List<ChatMessageEvent>();

                history.Add(chatEvent);

                // 최근 100개만 유지
                if (history.Count > 100)
                {
                    history = history.Skip(history.Count - 100).ToList();
                }

                await _cacheService.SetAsync(historyKey, history, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                LogError($"Failed to save chat history: {ex.Message}");
            }
        }

        /// <summary>
        /// 채팅 히스토리 조회
        /// </summary>
        public async Task<List<ChatMessageEvent>> GetChatHistoryAsync(string channelId, int count = 50)
        {
            try
            {
                var historyKey = $"history:{channelId}";
                var history = await _cacheService.GetAsync<List<ChatMessageEvent>>(historyKey);

                if (history == null || history.Count == 0)
                    return new List<ChatMessageEvent>();

                return history.TakeLast(count).ToList();
            }
            catch (Exception ex)
            {
                LogError($"Failed to get chat history: {ex.Message}");
                return new List<ChatMessageEvent>();
            }
        }

        /// <summary>
        /// 채널 사용자 목록 조회
        /// </summary>
        public List<string> GetChannelUsers(string channelId)
        {
            if (_channelUsers.TryGetValue(channelId, out var users))
            {
                lock (users)
                {
                    return users.ToList();
                }
            }
            return new List<string>();
        }

        /// <summary>
        /// 채널 사용자 수 업데이트
        /// </summary>
        private async Task UpdateChannelUserCountAsync(string channelId)
        {
            try
            {
                var count = _channelUsers.TryGetValue(channelId, out var users) ? users.Count : 0;
                await _cacheService.SetAsync($"channelcount:{channelId}", count, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                LogError($"Failed to update channel user count: {ex.Message}");
            }
        }

        /// <summary>
        /// 메시지 필터링 (욕설, 스팸 등)
        /// </summary>
        private string FilterMessage(string message)
        {
            // 간단한 필터링 예제 (실제로는 더 정교한 필터링 필요)
            if (string.IsNullOrWhiteSpace(message))
                return "";

            // 최대 길이 제한
            if (message.Length > 500)
                message = message.Substring(0, 500);

            // 연속된 특수문자 제거
            // message = Regex.Replace(message, @"[!@#$%^&*]{4,}", "***");

            return message;
        }

        /// <summary>
        /// 글로벌 채팅 메시지 브로드캐스트
        /// </summary>
        public async Task<ChatSendResult> BroadcastGlobalMessageAsync(string userId, string userName, string message)
        {
            return await SendMessageAsync("global", userId, userName, message);
        }

        /// <summary>
        /// 특정 사용자에게 Direct Message 전송
        /// </summary>
        public async Task<ChatSendResult> SendDirectMessageAsync(string fromUserId, string fromUserName, string toUserId, string message)
        {
            try
            {
                // Rate Limiting
                var rateLimitResult = await _rateLimiter.CheckUserRateLimitAsync(
                    $"dm:{fromUserId}",
                    RateLimitProfiles.ChatMessage.maxMessages,
                    RateLimitProfiles.ChatMessage.window);

                if (!rateLimitResult.IsAllowed)
                {
                    return new ChatSendResult
                    {
                        Success = false,
                        ErrorMessage = "Too many messages. Please slow down.",
                        RateLimited = true
                    };
                }

                var chatEvent = new ChatMessageEvent
                {
                    ChannelId = $"dm:{fromUserId}:{toUserId}",
                    UserId = fromUserId,
                    UserName = fromUserName,
                    Message = FilterMessage(message),
                    Timestamp = DateTime.UtcNow,
                    MessageType = ChatMessageType.DirectMessage
                };

                // 상대방의 개인 채널로 전송
                var channel = PubSubChannels.GetUserChannel(toUserId);
                var success = await _pubSubService.PublishAsync(channel, new PubSubMessage
                {
                    MessageType = "DirectMessage",
                    SenderId = fromUserId,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(chatEvent)
                });

                return new ChatSendResult { Success = success };
            }
            catch (Exception ex)
            {
                LogError($"Failed to send direct message: {ex.Message}");
                return new ChatSendResult { Success = false, ErrorMessage = "Internal error" };
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _channelUsers.Clear();
            _messageHandlers.Clear();
            _isDisposed = true;

            LogInfo("RedisChatService disposed");
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisChat] {message}");
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisChat] WARNING: {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisChat] ERROR: {message}");
        }
    }

    /// <summary>
    /// 채팅 메시지 이벤트
    /// </summary>
    public class ChatMessageEvent
    {
        public string ChannelId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public ChatMessageType MessageType { get; set; }
    }

    /// <summary>
    /// 채팅 메시지 타입
    /// </summary>
    public enum ChatMessageType
    {
        User,           // 일반 사용자 메시지
        System,         // 시스템 메시지
        DirectMessage,  // DM
        Announcement    // 공지사항
    }

    /// <summary>
    /// 채팅 전송 결과
    /// </summary>
    public class ChatSendResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RateLimited { get; set; }
    }
}
