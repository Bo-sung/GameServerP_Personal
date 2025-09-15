using System.Net.Sockets;

namespace Server
{
    // User 클래스를 확장하여 메시지 처리 기능 추가
    public class EnhancedUser : User
    {
        public string Username { get; set; } = "";
        public bool IsLoggedIn { get; set; } = false;
        public int Index { get; set; }

        private NetworkService _networkService;
        private bool _isRunning = true;

        public EnhancedUser(TcpClient client, int index, NetworkService networkService) : base(client)
        {
            Index = index;
            _networkService = networkService;

            // 비동기로 메시지 수신 시작
            _ = Task.Run(ReceiveMessagesAsync);
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_isRunning && m_TCP_Client?.Connected == true)
                {
                    var message = ReceiveString();
                    if (message == null)
                    {
                        Console.WriteLine($"[USER {Index}] 연결 종료");
                        break;
                    }

                    Console.WriteLine($"[USER {Index}] 메시지 수신: {message}");
                    await ProcessMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER {Index}] 수신 오류: {ex.Message}");
            }
            finally
            {
                _networkService.RemoveUser(Index);
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                var protocol = ParseProtocol(message);
                if (protocol != null)
                {
                    await HandleProtocolAsync(protocol);
                }
                else
                {
                    Console.WriteLine($"[USER {Index}] 프로토콜 파싱 실패: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER {Index}] 메시지 처리 오류: {ex.Message}");
            }
        }

        private Protocol ParseProtocol(string message)
        {
            try
            {
                var tokIndex = message.IndexOf("!@");
                if (tokIndex == -1)
                {
                    var endIndex = message.IndexOf("#@@");
                    if (endIndex != -1)
                    {
                        if (int.TryParse(message.Substring(0, endIndex), out int idOnly))
                        {
                            return new Protocol(idOnly, Protocol.ProtocolType.ToServer);
                        }
                    }
                    return null;
                }

                var idPart = message.Substring(0, tokIndex);
                if (!int.TryParse(idPart, out int id))
                {
                    return null;
                }

                var protocol = new Protocol(id, Protocol.ProtocolType.ToServer);
                var paramPart = message.Substring(tokIndex);
                var parameters = protocol.FromString(paramPart);

                foreach (var param in parameters)
                {
                    protocol.SetParam(param.Key, param.Value);
                }

                return protocol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER {Index}] 프로토콜 파싱 오류: {ex.Message}");
                return null;
            }
        }

        private async Task HandleProtocolAsync(Protocol protocol)
        {
            switch (protocol.ID)
            {
                case Protocol.IDs.USER_LOGIN_REQUEST:
                    await HandleLoginRequestAsync(protocol);
                    break;
                case Protocol.IDs.USER_LOGOUT_REQUEST:
                    await HandleLogoutRequestAsync(protocol);
                    break;
                case Protocol.IDs.PING_REQUEST:
                    await HandlePingRequestAsync(protocol);
                    break;
                default:
                    Console.WriteLine($"[USER {Index}] 알 수 없는 프로토콜 ID: {protocol.ID}");
                    break;
            }
        }

        private async Task HandleLoginRequestAsync(Protocol protocol)
        {
            var username = protocol.Parameter.GetValueOrDefault(0, "").ToString();
            var password = protocol.Parameter.GetValueOrDefault(1, "").ToString();

            Console.WriteLine($"[USER {Index}] 로그인 요청: {username}");

            // 간단한 로그인 검증 (실제로는 DB 조회 등)
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                // 로그인 성공
                Username = username;
                IsLoggedIn = true;

                var response = new Protocol(Protocol.IDs.USER_LOGIN_SUCCESS, Protocol.ProtocolType.ToClient);
                response.SetParam(0, username);
                response.SetParam(1, "로그인 성공");
                response.SetParam(2, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                // 서버 목록 추가
                response.SetParam(3, $"{ServerInfo.Server1.Type}:{ServerInfo.Server1.Address}");
                response.SetParam(4, $"{ServerInfo.Server2.Type}:{ServerInfo.Server2.Address}");

                Send(response, true);
                Console.WriteLine($"[USER {Index}] 로그인 성공: {username}");
            }
            else
            {
                // 로그인 실패
                var response = new Protocol(Protocol.IDs.USER_LOGIN_FAIL, Protocol.ProtocolType.ToClient);
                response.SetParam(0, "아이디 또는 패스워드가 비어있습니다");

                Send(response, true);
                Console.WriteLine($"[USER {Index}] 로그인 실패: 빈 정보");
            }
        }

        private async Task HandleLogoutRequestAsync(Protocol protocol)
        {
            Console.WriteLine($"[USER {Index}] 로그아웃 요청: {Username}");

            var response = new Protocol(Protocol.IDs.USER_LOGOUT_SUCCESS, Protocol.ProtocolType.ToClient);
            response.SetParam(0, "로그아웃 완료");
            response.SetParam(1, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            Send(response, true);

            IsLoggedIn = false;
            Username = "";
            Console.WriteLine($"[USER {Index}] 로그아웃 완료");
        }

        private async Task HandlePingRequestAsync(Protocol protocol)
        {
            var response = new Protocol(Protocol.IDs.PONG_RESPONSE, Protocol.ProtocolType.ToClient);
            response.SetParam(0, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Send(response, true);
            Console.WriteLine($"[USER {Index}] Ping 응답 전송");
        }

        public override void Close()
        {
            _isRunning = false;
            base.Close();
        }
    }
}