using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonLib.Redis
{
    /// <summary>
    /// Redis Pub/Sub 기반 메시징 서비스
    /// 서버 간 실시간 메시지 전송 및 이벤트 브로드캐스팅
    /// </summary>
    public class RedisPubSubService : IDisposable
    {
        private readonly RedisService _redisService;
        private readonly ConcurrentDictionary<string, Action<string>> _channelHandlers;
        private readonly ConcurrentDictionary<string, Action<PubSubMessage>> _typedHandlers;
        private bool _isDisposed;

        public RedisPubSubService(RedisService redisService)
        {
            _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
            _channelHandlers = new ConcurrentDictionary<string, Action<string>>();
            _typedHandlers = new ConcurrentDictionary<string, Action<PubSubMessage>>();
        }

        /// <summary>
        /// 메시지 발행 (Raw String)
        /// </summary>
        public async Task<bool> PublishAsync(string channel, string message)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning($"Redis not connected, message not published to {channel}");
                    return false;
                }

                var subscriber = _redisService.Subscriber;
                var receiverCount = await subscriber.PublishAsync(channel, message);

                LogInfo($"Published to '{channel}': {receiverCount} receivers");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to publish to {channel}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 메시지 발행 (Typed Object)
        /// </summary>
        public async Task<bool> PublishAsync(string channel, PubSubMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                return await PublishAsync(channel, json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to serialize and publish to {channel}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채널 구독 (Raw String Handler)
        /// </summary>
        public async Task<bool> SubscribeAsync(string channel, Action<string> handler)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning($"Redis not connected, cannot subscribe to {channel}");
                    return false;
                }

                var subscriber = _redisService.Subscriber;

                await subscriber.SubscribeAsync(channel, (ch, message) =>
                {
                    try
                    {
                        handler(message.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in handler for channel {channel}: {ex.Message}");
                    }
                });

                _channelHandlers[channel] = handler;
                LogInfo($"Subscribed to channel: {channel}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to subscribe to {channel}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채널 구독 (Typed Object Handler)
        /// </summary>
        public async Task<bool> SubscribeAsync(string channel, Action<PubSubMessage> handler)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning($"Redis not connected, cannot subscribe to {channel}");
                    return false;
                }

                var subscriber = _redisService.Subscriber;

                await subscriber.SubscribeAsync(channel, (ch, message) =>
                {
                    try
                    {
                        var pubSubMessage = JsonSerializer.Deserialize<PubSubMessage>(message.ToString());
                        if (pubSubMessage != null)
                        {
                            handler(pubSubMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error deserializing or handling message in {channel}: {ex.Message}");
                    }
                });

                _typedHandlers[channel] = handler;
                LogInfo($"Subscribed to channel (typed): {channel}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to subscribe to {channel}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 패턴 기반 구독 (예: "chat:*")
        /// </summary>
        public async Task<bool> SubscribePatternAsync(string pattern, Action<string, string> handler)
        {
            try
            {
                if (!_redisService.IsConnected)
                {
                    LogWarning($"Redis not connected, cannot subscribe to pattern {pattern}");
                    return false;
                }

                var subscriber = _redisService.Subscriber;

                await subscriber.SubscribeAsync(pattern, (channel, message) =>
                {
                    try
                    {
                        handler(channel.ToString(), message.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in pattern handler for {pattern}: {ex.Message}");
                    }
                });

                LogInfo($"Subscribed to pattern: {pattern}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to subscribe to pattern {pattern}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채널 구독 해제
        /// </summary>
        public async Task<bool> UnsubscribeAsync(string channel)
        {
            try
            {
                if (!_redisService.IsConnected)
                    return false;

                var subscriber = _redisService.Subscriber;
                await subscriber.UnsubscribeAsync(channel);

                _channelHandlers.TryRemove(channel, out _);
                _typedHandlers.TryRemove(channel, out _);

                LogInfo($"Unsubscribed from channel: {channel}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to unsubscribe from {channel}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 모든 채널 구독 해제
        /// </summary>
        public async Task UnsubscribeAllAsync()
        {
            try
            {
                if (!_redisService.IsConnected)
                    return;

                var subscriber = _redisService.Subscriber;
                await subscriber.UnsubscribeAllAsync();

                _channelHandlers.Clear();
                _typedHandlers.Clear();

                LogInfo("Unsubscribed from all channels");
            }
            catch (Exception ex)
            {
                LogError($"Failed to unsubscribe all: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버 브로드캐스트 (모든 서버에 메시지 전송)
        /// </summary>
        public async Task<bool> BroadcastToServersAsync(ServerBroadcastMessage message)
        {
            try
            {
                return await PublishAsync(PubSubChannels.ServerBroadcast, new PubSubMessage
                {
                    MessageType = "ServerBroadcast",
                    SenderId = message.SenderId,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(message)
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to broadcast to servers: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 채팅 메시지 발행
        /// </summary>
        public async Task<bool> PublishChatMessageAsync(string roomId, ChatMessage chatMessage)
        {
            try
            {
                var channel = PubSubChannels.GetChatChannel(roomId);
                return await PublishAsync(channel, new PubSubMessage
                {
                    MessageType = "Chat",
                    SenderId = chatMessage.SenderId,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(chatMessage)
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to publish chat message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 사용자 상태 변경 알림
        /// </summary>
        public async Task<bool> PublishUserStatusAsync(UserStatusMessage statusMessage)
        {
            try
            {
                return await PublishAsync(PubSubChannels.UserStatus, new PubSubMessage
                {
                    MessageType = "UserStatus",
                    SenderId = statusMessage.UserId,
                    Timestamp = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(statusMessage)
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to publish user status: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                UnsubscribeAllAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore errors during disposal
            }

            _isDisposed = true;
            LogInfo("RedisPubSubService disposed");
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisPubSub] {message}");
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisPubSub] WARNING: {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [RedisPubSub] ERROR: {message}");
        }
    }

    /// <summary>
    /// Pub/Sub 메시지 모델
    /// </summary>
    public class PubSubMessage
    {
        public string MessageType { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Payload { get; set; } = string.Empty;
    }

    /// <summary>
    /// 서버 브로드캐스트 메시지
    /// </summary>
    public class ServerBroadcastMessage
    {
        public string SenderId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // "ServerStart", "ServerShutdown", "Maintenance"
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 채팅 메시지
    /// </summary>
    public class ChatMessage
    {
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 사용자 상태 메시지
    /// </summary>
    public class UserStatusMessage
    {
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Online", "Offline", "InGame", "Away"
        public string ServerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pub/Sub 채널 상수
    /// </summary>
    public static class PubSubChannels
    {
        public const string ServerBroadcast = "server:broadcast";
        public const string UserStatus = "user:status";
        public const string ChatGlobal = "chat:global";

        public static string GetChatChannel(string roomId) => $"chat:room:{roomId}";
        public static string GetUserChannel(string userId) => $"user:{userId}";
    }
}
