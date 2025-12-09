using AuthServer.Models;
using System.Collections.Concurrent;

namespace AuthServer.Services
{
    /// <summary>
    /// 게임 서버 등록 및 관리 서비스
    /// Lobby/Game 서버들을 등록하고 목록을 관리
    /// </summary>
    public class ServerRegistryService
    {
        private readonly ConcurrentDictionary<string, ServerInfo> _servers = new();
        private readonly string _serverSecretKey;
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(2);

        public ServerRegistryService(string serverSecretKey)
        {
            _serverSecretKey = serverSecretKey;

            // 백그라운드에서 타임아웃된 서버 정리
            _ = Task.Run(CleanupTimedOutServersAsync);
        }

        /// <summary>
        /// 서버 등록
        /// </summary>
        public bool RegisterServer(RegisterServerRequest request)
        {
            // 서버 인증
            if (request.SecretKey != _serverSecretKey)
            {
                Console.WriteLine($"[ServerRegistry] Registration failed - Invalid secret key from {request.ServerName}");
                return false;
            }

            var serverInfo = new ServerInfo
            {
                ServerName = request.ServerName,
                ServerType = request.ServerType,
                Host = request.Host,
                Port = request.Port,
                UdpPort = request.UdpPort,
                MaxCapacity = request.MaxCapacity,
                CurrentPlayers = 0,
                Status = "Online",
                LastHeartbeat = DateTime.UtcNow
            };

            _servers[request.ServerName] = serverInfo;
            Console.WriteLine($"[ServerRegistry] Server registered: {request.ServerName} ({request.ServerType}) at {request.Host}:{request.Port}");
            return true;
        }

        /// <summary>
        /// 서버 하트비트 업데이트
        /// </summary>
        public bool UpdateHeartbeat(ServerHeartbeatRequest request)
        {
            // 서버 인증
            if (request.SecretKey != _serverSecretKey)
            {
                return false;
            }

            if (_servers.TryGetValue(request.ServerName, out var server))
            {
                server.LastHeartbeat = DateTime.UtcNow;
                server.CurrentPlayers = request.CurrentPlayers;
                server.Status = request.Status;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 서버 등록 해제
        /// </summary>
        public bool UnregisterServer(string serverName, string secretKey)
        {
            if (secretKey != _serverSecretKey)
            {
                return false;
            }

            if (_servers.TryRemove(serverName, out _))
            {
                Console.WriteLine($"[ServerRegistry] Server unregistered: {serverName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 타입의 서버 목록 조회
        /// </summary>
        public List<ServerInfo> GetServersByType(string serverType)
        {
            return _servers.Values
                .Where(s => s.ServerType.Equals(serverType, StringComparison.OrdinalIgnoreCase))
                .Where(s => DateTime.UtcNow - s.LastHeartbeat < _heartbeatTimeout)
                .OrderBy(s => s.CurrentPlayers) // 인원 적은 순
                .ToList();
        }

        /// <summary>
        /// 모든 활성 서버 조회
        /// </summary>
        public List<ServerInfo> GetAllActiveServers()
        {
            return _servers.Values
                .Where(s => DateTime.UtcNow - s.LastHeartbeat < _heartbeatTimeout)
                .ToList();
        }

        /// <summary>
        /// 추천 Lobby 서버 조회 (인원 적고 상태 좋은 서버)
        /// </summary>
        public ServerInfo? GetRecommendedLobbyServer()
        {
            return GetServersByType("Lobby")
                .Where(s => s.Status == "Online" && s.CurrentPlayers < s.MaxCapacity)
                .OrderBy(s => (double)s.CurrentPlayers / s.MaxCapacity) // 가장 여유로운 서버
                .FirstOrDefault();
        }

        /// <summary>
        /// 타임아웃된 서버 정리 (백그라운드 작업)
        /// </summary>
        private async Task CleanupTimedOutServersAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                var timedOutServers = _servers.Values
                    .Where(s => DateTime.UtcNow - s.LastHeartbeat >= _heartbeatTimeout)
                    .ToList();

                foreach (var server in timedOutServers)
                {
                    if (_servers.TryRemove(server.ServerName, out _))
                    {
                        Console.WriteLine($"[ServerRegistry] Server timed out and removed: {server.ServerName}");
                    }
                }
            }
        }
    }
}
