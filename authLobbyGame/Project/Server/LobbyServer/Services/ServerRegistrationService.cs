using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LobbyServer.Services
{
    /// <summary>
    /// Auth 서버에 Lobby 서버를 등록하고 하트비트를 전송하는 서비스
    /// </summary>
    public class ServerRegistrationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _authServerUrl;
        private readonly string _serverName;
        private readonly string _serverType;
        private readonly string _host;
        private readonly int _port;
        private readonly int _maxCapacity;
        private readonly string _secretKey;
        private readonly bool _enableRegistration;
        private readonly int _heartbeatIntervalSeconds;

        private Timer? _heartbeatTimer;
        private int _currentPlayers = 0;
        private bool _isRegistered = false;

        public ServerRegistrationService(IConfiguration config, string serverName, string host, int port, int maxCapacity)
        {
            _authServerUrl = config.GetValue<string>("AuthServer:BaseUrl") ?? "http://localhost:5000";
            _secretKey = config.GetValue<string>("AuthServer:ServerSecretKey") ?? "";
            _enableRegistration = config.GetValue<bool>("AuthServer:EnableRegistration");
            _heartbeatIntervalSeconds = config.GetValue<int>("AuthServer:HeartbeatIntervalSeconds");
            if (_heartbeatIntervalSeconds <= 0) _heartbeatIntervalSeconds = 30;

            _serverName = serverName;
            _serverType = "Lobby";
            _host = host;
            _port = port;
            _maxCapacity = maxCapacity;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_authServerUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Auth 서버에 서버 등록 시작
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (!_enableRegistration)
            {
                LogWithTimestamp("[ServerRegistration] Registration disabled in config");
                return false;
            }

            if (string.IsNullOrEmpty(_secretKey))
            {
                LogWithTimestamp("[ServerRegistration] Secret key not configured");
                return false;
            }

            // 서버 등록
            _isRegistered = await RegisterServerAsync();

            if (_isRegistered)
            {
                // 하트비트 시작
                StartHeartbeat();
                LogWithTimestamp($"[ServerRegistration] Started with {_heartbeatIntervalSeconds}s heartbeat interval");
            }

            return _isRegistered;
        }

        /// <summary>
        /// Auth 서버에 서버 등록
        /// </summary>
        private async Task<bool> RegisterServerAsync()
        {
            try
            {
                var requestData = new
                {
                    ServerName = _serverName,
                    ServerType = _serverType,
                    Host = _host,
                    Port = _port,
                    UdpPort = 0,
                    MaxCapacity = _maxCapacity,
                    SecretKey = _secretKey
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/server/register", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    LogWithTimestamp($"[ServerRegistration] Successfully registered: {_serverName} at {_host}:{_port}");
                    return true;
                }
                else
                {
                    LogWithTimestamp($"[ServerRegistration] Registration failed: {response.StatusCode} - {responseBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[ServerRegistration] Registration error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 하트비트 전송 시작
        /// </summary>
        private void StartHeartbeat()
        {
            _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(),
                null,
                TimeSpan.FromSeconds(_heartbeatIntervalSeconds),
                TimeSpan.FromSeconds(_heartbeatIntervalSeconds));
        }

        /// <summary>
        /// Auth 서버에 하트비트 전송
        /// </summary>
        private async Task SendHeartbeatAsync()
        {
            if (!_isRegistered)
                return;

            try
            {
                var requestData = new
                {
                    ServerName = _serverName,
                    CurrentPlayers = _currentPlayers,
                    Status = "Online",
                    SecretKey = _secretKey
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/server/heartbeat", content);

                if (!response.IsSuccessStatusCode)
                {
                    LogWithTimestamp($"[ServerRegistration] Heartbeat failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[ServerRegistration] Heartbeat error: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 플레이어 수 업데이트
        /// </summary>
        public void UpdatePlayerCount(int playerCount)
        {
            _currentPlayers = playerCount;
        }

        /// <summary>
        /// Auth 서버에서 서버 등록 해제
        /// </summary>
        public async Task StopAsync()
        {
            _heartbeatTimer?.Dispose();

            if (!_isRegistered)
                return;

            try
            {
                var requestData = new
                {
                    ServerName = _serverName,
                    SecretKey = _secretKey
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/server/unregister", content);

                if (response.IsSuccessStatusCode)
                {
                    LogWithTimestamp($"[ServerRegistration] Successfully unregistered: {_serverName}");
                }
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[ServerRegistration] Unregistration error: {ex.Message}");
            }

            _isRegistered = false;
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _httpClient?.Dispose();
        }

        private void LogWithTimestamp(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }
    }
}
