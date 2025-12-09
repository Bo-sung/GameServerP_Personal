using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LobbyServer.Core.Game.Managers;
using LobbyServer.Core.Game.Session;
using LobbyServer.Database;
using LobbyServer.Services;
using LobbyServer.Utils;
using Microsoft.Extensions.Configuration;

namespace LobbyServer
{
    class Program
    {
        static async Task Main(string[] _args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    Lobby Server Starting...");
            Console.WriteLine("========================================");
            Console.WriteLine();

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // CommonLib은 자체적으로 설정을 로드할 것으로 가정합니다.
            // DBManager.Instance.UpdateTable() 호출 시 CommonLib.AppConfig가 사용됩니다.
            DBManager dBManager = DBManager.Instance;
            dBManager.UpdateTable();

            TcpListener server = null;
            GameServerRegistrar? registrar = null;
            AuthServerClient? authServerClient = null;
            ServerRegistrationService? serverRegistration = null;

            try
            {
                string host = config.GetValue<string>("Server:Host");
                int port = config.GetValue<int>("Server:Port");
                string serverName = config.GetValue<string>("Server:Name") ?? $"LobbyServer-{Environment.MachineName}";
                int maxCapacity = config.GetValue<int>("Server:MaxCapacity");
                if (maxCapacity <= 0) maxCapacity = 100;

                // Auth 서버 클라이언트 초기화
                try
                {
                    authServerClient = new AuthServerClient(config);
                    bool authServerConnected = await authServerClient.CheckConnectionAsync();
                    if (authServerConnected)
                    {
                        LogWithTimestamp("[Server] Auth Server connection established");
                    }
                    else
                    {
                        LogWithTimestamp("[WARNING] Auth Server is not reachable. JWT authentication will not be available.");
                        LogWithTimestamp("[WARNING] Falling back to direct DB authentication only.");
                    }
                }
                catch (Exception ex)
                {
                    LogWithTimestamp($"[WARNING] Failed to initialize Auth Server Client: {ex.Message}");
                    LogWithTimestamp("[WARNING] JWT authentication will not be available.");
                }

                // Auth 서버에 Lobby 서버 등록 (새로운 서비스 디스커버리 방식)
                try
                {
                    serverRegistration = new ServerRegistrationService(config, serverName, host, port, maxCapacity);
                    bool registered = await serverRegistration.StartAsync();
                    if (registered)
                    {
                        LogWithTimestamp($"[Server] Registered to Auth Server as '{serverName}'");
                    }
                }
                catch (Exception ex)
                {
                    LogWithTimestamp($"[WARNING] Failed to register to Auth Server: {ex.Message}");
                }

                // 게임 서버 등록기 초기화 및 시작 (기존 방식 - 호환성 유지)
                try
                {
                    registrar = new GameServerRegistrar(serverName, host, port, udpPort: 0, maxCapacity: maxCapacity);
                    registrar.Start();
                }
                catch (Exception ex)
                {
                    LogWithTimestamp($"[Server] Failed to start GameServerRegistrar: {ex.Message}");
                }

                IPAddress localAddr;

                // 1. IP 주소 문자열인지 먼저 확인 (0.0.0.0, 127.0.0.1 등)
                if (IPAddress.TryParse(host, out IPAddress parsedIp))
                {
                    // 0.0.0.0인 경우 IPAddress.Any로 변환 (명시적 처리)
                    if (parsedIp.Equals(IPAddress.Parse("0.0.0.0")))
                    {
                        localAddr = IPAddress.Any;
                    }
                    else
                    {
                        localAddr = parsedIp;
                    }
                }
                else
                {
                    // 2. 호스트명인 경우 DNS 조회 (localhost 등)
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length == 0)
                    {
                        LogWithTimestamp($"[ERROR] Could not resolve host: {host}");
                        return;
                    }
                    localAddr = addresses[0];
                }

                server = new TcpListener(localAddr, port);
                server.Start();

                LogWithTimestamp($"[Server] Listening on {server.LocalEndpoint}");

                // 루프백 주소로 바인딩된 경우 경고 출력
                if (IPAddress.IsLoopback(((IPEndPoint)server.LocalEndpoint).Address))
                {
                    LogWithTimestamp("[WARNING] Server is bound to loopback address (127.0.0.1). External connections will fail.");
                    LogWithTimestamp("[WARNING] Please check 'appsettings.json' and set 'Server:Host' to '0.0.0.0'.");
                }

                LogWithTimestamp("[Server] Waiting for clients to connect...");
                LogWithTimestamp("");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    ClientSession session = new ClientSession(client, authServerClient);
                    _ = Task.Run(async () => await session.StartAsync());
                }
            }
            catch (SocketException e)
            {
                LogWithTimestamp($"[Server] SocketException: {e}");
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Server] Exception: {e}");
            }
            finally
            {
                server?.Stop();
                try
                {
                    registrar?.Stop();
                }
                catch { }
                try
                {
                    serverRegistration?.StopAsync().Wait();
                }
                catch { }
                authServerClient?.Dispose();
                serverRegistration?.Dispose();
                RoomManager.Instance.Shutdown();
                LogWithTimestamp("[Server] Shutdown complete");
            }
        }

        /// <summary>
        /// 타임스탬프가 있는 로그 출력
        /// </summary>
        static void LogWithTimestamp(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }
    }
}