using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using CommonLib;
using CommonLib.Database;

namespace LobbyServer.Utils
{
    /// <summary>
    /// 게임 서버 등록 및 하트비트 전송 유틸리티
    /// - 시작 시 `game_servers` 테이블에 등록
    /// - 주기적으로 `last_heartbeat` 갱신
    /// </summary>
    public class GameServerRegistrar : IDisposable
    {
        private readonly string _serverId;
        private readonly string _serverName;
        private readonly string _ipAddress;
        private readonly int _tcpPort;
        private readonly int _udpPort;
        private readonly int _maxCapacity;
        private readonly GameServerDbManager _dbManager;
        private Timer? _heartbeatTimer;
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
        private bool _running = false;

        public string ServerId => _serverId;

        public GameServerRegistrar(string serverName, string ipAddress, int tcpPort, int udpPort = 0, int maxCapacity = 100)
        {
            _serverId = Guid.NewGuid().ToString();
            _serverName = serverName ?? "GenericGameServer";
            _ipAddress = ipAddress ?? "127.0.0.1";
            _tcpPort = tcpPort;
            _udpPort = udpPort;
            _maxCapacity = maxCapacity;
            _dbManager = new GameServerDbManager(AppConfig.Instance.TableDatabaseConnectionString);
        }

        public void Start()
        {
            if (_running) return;
            _running = true;

            try
            {
                RegisterOrUpdate();

                // 시작 후 즉시 하트비트, 이후 주기적 갱신
                _heartbeatTimer = new Timer(_ => SendHeartbeat(), null, TimeSpan.Zero, _heartbeatInterval);
                Console.WriteLine($"[GameServerRegistrar] Registered server {_serverId} and started heartbeat.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerRegistrar] Start error: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;

            try
            {
                _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                SetStatusOffline();
                _heartbeatTimer?.Dispose();
                Console.WriteLine($"[GameServerRegistrar] Stopped and set offline: {_serverId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerRegistrar] Stop error: {ex.Message}");
            }
        }

        private void RegisterOrUpdate()
        {
            _dbManager.RegisterOrUpdateServerAsync(_serverId, _serverName, _ipAddress, _tcpPort, _udpPort, _maxCapacity).Wait();
        }

        private void SendHeartbeat()
        {
            try
            {
                _dbManager.SendHeartbeatAsync(_serverId).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerRegistrar] Heartbeat error: {ex.Message}");
            }
        }

        private void SetStatusOffline()
        {
            try
            {
                _dbManager.SetStatusOfflineAsync(_serverId).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerRegistrar] Set offline error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
