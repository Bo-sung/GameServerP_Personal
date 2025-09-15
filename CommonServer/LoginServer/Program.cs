using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    public class ServerInfo
    {
        public string Type { get; set; } = ""; // GAME, CHAT, etc.
        public string Address { get; set; } = ""; // IP:PORT

        public static ServerInfo Server1 = new ServerInfo() { Type = "GAME", Address = $"{IPAddress.Any}:{5001}" };
        public static ServerInfo Server2 = new ServerInfo() { Type = "CHAT", Address = $"{IPAddress.Any}:{5002}" };
    }

    public class NetworkService
    {
        public const int DEFAULT_PORT = 5000;
        public const int MAX_USERS = 100;

        private IPAddress _iPAddress = IPAddress.Any;
        private int _port = DEFAULT_PORT;

        private EnhancedUser[] users = new EnhancedUser[MAX_USERS];
        private int nextUserIndex = 0;

        private TcpListener listener;

        public NetworkService(string[] args)
        {
            _port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : DEFAULT_PORT;
            listener = new TcpListener(_iPAddress, _port);
        }

        public async Task StartAsync()
        {
            listener.Start();
            Console.WriteLine($"[SERVER] Listening on 0.0.0.0:{_port} ...");

            while (true)
            {
                try
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();

                    if (AddUser(tcpClient))
                    {
                        Console.WriteLine($"[SERVER] 새 사용자 접속 성공");
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] 사용자 접속 실패");
                        tcpClient?.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER] 접속 처리 오류: {ex.Message}");
                }
            }
        }

        public void Start()
        {
            listener.Start();
            Console.WriteLine($"[SERVER] Listening on 0.0.0.0:{_port} ...");

            // 기존 동기 방식도 지원 (기존 코드 호환성)
            while (true)
            {
                try
                {
                    var tcpClient = listener.AcceptTcpClient();

                    if (AddUser(tcpClient))
                    {
                        Console.WriteLine($"[SERVER] 새 사용자 접속 성공");
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] 사용자 접속 실패");
                        tcpClient?.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SERVER] 접속 처리 오류: {ex.Message}");
                }
            }
        }

        private void SetNextUserIndex()
        {
            // 현재 인덱스부터 최대값까지 탐색
            for (int i = nextUserIndex; i < MAX_USERS; i++)
            {
                if (users[i] == null)
                {
                    nextUserIndex = i;
                    return;
                }
            }

            // 0부터 현재 인덱스까지 탐색
            for (int i = 0; i < nextUserIndex; i++)
            {
                if (users[i] == null)
                {
                    nextUserIndex = i;
                    return;
                }
            }

            // 빈칸 없다 == 가득참. 풀 처리 리턴
            nextUserIndex = -1;
            return;
        }

        protected object AddUserLock = new object();
        protected bool AddUser(TcpClient client)
        {
            // 방어 코드들
            if (client == null)
            {
                Console.WriteLine("[SERVER] client is null");
                return false;
            }

            if (nextUserIndex == -1)
            {
                Console.WriteLine("[SERVER] User Is Full");
                return false;
            }

            if (nextUserIndex >= MAX_USERS || nextUserIndex < 0)
            {
                Console.WriteLine("[SERVER] OutOfRange nextUserIndex. Invalid nextUserIndex");
                return false;
            }

            int userindex = nextUserIndex;
            lock (AddUserLock)
            {
                if (users[nextUserIndex] != null)
                {
                    Console.WriteLine("[SERVER] Already User Exist At nextUserIndex. Invalid nextUserIndex");
                    return false;
                }

                users[nextUserIndex] = new EnhancedUser(client, nextUserIndex, this);
                SetNextUserIndex();
            }

            // USER_ENTER_SUCCESS 전송
            Protocol proto = Protocol.USER_ENTER_SUCCESS;
            proto.SetParam(0, "접속을 환영합니다");
            users[userindex].Send(proto, true);

            Console.WriteLine($"[SERVER] 사용자 추가 완료: Index={userindex}");
            return true;
        }

        public bool RemoveUser(int index)
        {
            if (index >= MAX_USERS || index < 0)
            {
                Console.WriteLine("[SERVER] OutOfRange index. Invalid index");
                return false;
            }

            lock (AddUserLock)
            {
                if (users[index] == null)
                {
                    Console.WriteLine("[SERVER] No User At Index. Invalid index");
                    return false;
                }

                var user = users[index];
                Console.WriteLine($"[SERVER] 사용자 제거: Index={index}, Username={user.Username}");

                user.Close();
                users[index] = null;

                // 빈 슬롯이 생겼으므로 nextUserIndex 업데이트
                if (nextUserIndex == -1 || index < nextUserIndex)
                {
                    nextUserIndex = index;
                }
            }

            return true;
        }

        public void BroadcastMessage(Protocol protocol, int excludeUserIndex = -1)
        {
            lock (AddUserLock)
            {
                for (int i = 0; i < MAX_USERS; i++)
                {
                    if (users[i] != null && i != excludeUserIndex && users[i].IsLoggedIn)
                    {
                        users[i].Send(protocol, true);
                    }
                }
            }
        }

        public EnhancedUser GetUser(int index)
        {
            if (index >= 0 && index < MAX_USERS)
            {
                return users[index];
            }
            return null;
        }

        public int GetUserCount()
        {
            lock (AddUserLock)
            {
                int count = 0;
                for (int i = 0; i < MAX_USERS; i++)
                {
                    if (users[i] != null)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int GetLoggedInUserCount()
        {
            lock (AddUserLock)
            {
                int count = 0;
                for (int i = 0; i < MAX_USERS; i++)
                {
                    if (users[i] != null && users[i].IsLoggedIn)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
    }

    /// <summary>
    /// 로그인 결과를 담는 클래스 (기존 코드와의 호환성)
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Username { get; set; } = "";
        public string Message { get; set; } = "";
        public List<ServerInfo> Servers { get; set; } = new List<ServerInfo>();
    }

    internal class Program
    {
        static NetworkService networkMain;

        static async Task Main(string[] args)
        {
            networkMain = new NetworkService(args);

            Console.WriteLine("[SERVER] 로그인 서버 시작");

            await networkMain.StartAsync();
        }
    }
}