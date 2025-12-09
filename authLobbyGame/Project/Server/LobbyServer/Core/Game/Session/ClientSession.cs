using LobbyServer.Core.Game.Entities;
using LobbyServer.Core.Game.Managers;
using LobbyServer.Database;
using LobbyServer.Network;
using LobbyServer.Services;
using CommonLib;
using MySqlX.XDevAPI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LobbyServer.Network.ProtocolHandler;

namespace LobbyServer.Core.Game.Session
{
    /// <summary>
    /// 클라이언트 세션 관리 클래스
    /// </summary>
    public class ClientSession
    {
        public string SessionId { get; private set; }
        public TcpClient TcpClient { get; private set; }
        public Entities.Room? CurrentRoom { get; set; }
        public UserInfo UserInfo => m_userInfo;

        private NetworkStream m_stream;
        private bool m_isConnected = true;
        private UserInfo m_userInfo;
        private readonly object m_sendLock = new object();

        // 타임아웃 관련
        private DateTime m_lastActivityTime;
        protected Timer? m_timeoutCheckTimer;
        protected const int TIMEOUT_SECONDS = 30;           // 30초 동안 응답 없으면 타임아웃
        protected const int TIMEOUT_CHECK_INTERVAL = 5000;  // 5초마다 체크

        // 프로토콜 핸들러
        protected ProtocolHandler m_protocolHandler;

        // 세션 생성 시간 기록용
        private readonly Stopwatch m_sessionTimer;
        private readonly DB_Auth m_authDB;
        private readonly AuthServerClient? m_authServerClient;

        public ClientSession(TcpClient _client, AuthServerClient? authServerClient = null)
        {
            TcpClient = _client;
            TcpClient.NoDelay = true;

            m_stream = _client.GetStream();
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            m_lastActivityTime = DateTime.UtcNow;
            m_authDB = new DB_Auth();
            m_authServerClient = authServerClient;

            // 타이머 시작
            m_sessionTimer = Stopwatch.StartNew();

            // 프로토콜 핸들러 초기화 및 등록
            m_protocolHandler = new ProtocolHandler();
            RegisterProtocolHandlers();

            LogWithTimestamp($"[Session {SessionId}] Created");
        }

        // 타임스탬프 로그 헬퍼 메서드
        private void LogWithTimestamp(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var elapsed = m_sessionTimer?.ElapsedMilliseconds ?? 0;
            Console.WriteLine($"[{timestamp}] [{elapsed,6}ms] {message}");
        }

        /// <summary>
        /// 프로토콜 핸들러 등록
        /// </summary>
        protected virtual void RegisterProtocolHandlers()
        {
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.HEARTBEAT, HandleHeartbeat);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_REGISTER, Handle_RequestRegister);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_REGISTER_AUTO, Handle_RequestRegisterAuto);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_LOGIN, Handle_RequestLogin);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_LOGIN_WITH_TOKEN, Handle_RequestLoginWithToken);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_LOGOUT, Handle_RequestLogout);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_JOIN_LOBBY, Handle_RequestJoinLobby);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REFRESH_LOBBY, Handle_RefreshLobby);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_CREATE_ROOM, Handle_RequestCreateRoom);
            m_protocolHandler.RegisterHandler(CommonLib.ProtocolType.REQUEST_JOIN_ROOM, Handle_RequestJoinRoom);
        }

        public void RegisterProto(int Protocol, ProtocolHandlerDelegate handler)
        {
            m_protocolHandler.RegisterHandler(Protocol, handler);
        }

        public void UnRegisterProto(int Protocol)
        {
            m_protocolHandler.UnregisterHandler(Protocol);
        }

        /// <summary>
        /// 세션 시작 - 메시지 수신 루프
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                StartTimeoutCheck();
                await ReceiveLoop();
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Session {SessionId}] Error: {e.Message}");
            }
            finally
            {
                Cleanup();
            }
        }


        /// <summary>
        /// 타임아웃 체크 시작
        /// </summary>
        private void StartTimeoutCheck()
        {
            m_timeoutCheckTimer = new Timer(CheckTimeout, null,
                TIMEOUT_CHECK_INTERVAL, TIMEOUT_CHECK_INTERVAL);
        }

        private void CheckTimeout(object? _state)
        {
            if (!m_isConnected)
                return;

            TimeSpan timeSinceLastActivity = DateTime.UtcNow - m_lastActivityTime;

            if (timeSinceLastActivity.TotalSeconds > TIMEOUT_SECONDS)
            {
                LogWithTimestamp($"[Session {SessionId}] Timeout detected. Last activity: {timeSinceLastActivity.TotalSeconds:F1}s ago");
                Disconnect();
            }
        }

        /// <summary>
        /// 마지막 활동 시간 갱신
        /// </summary>
        public void UpdateLastActivity()
        {
            m_lastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 메시지 수신 루프
        /// </summary>
        private async Task ReceiveLoop()
        {
            byte[] lengthBuffer = new byte[4];

            try
            {
                while (m_isConnected)
                {
                    LogWithTimestamp($"[Session {SessionId}] ReceiveLoop iteration started");

                    // 1단계: 전체 메시지 크기 읽기 (크기 필드 포함)
                    int bytesRead = await m_stream.ReadAsync(lengthBuffer, 0, 4);
                    if (bytesRead == 0)
                    {
                        LogWithTimestamp($"[Session {SessionId}] Connection closed");
                        break;
                    }

                    int totalMessageSize = BitConverter.ToInt32(lengthBuffer, 0);
                    LogWithTimestamp($"[Session {SessionId}] Total message size: {totalMessageSize}");

                    UpdateLastActivity();

                    // 검증
                    if (totalMessageSize < 18 || totalMessageSize > 1024 * 1024)
                    {
                        LogWithTimestamp($"[Session {SessionId}] Invalid message size: {totalMessageSize}");
                        break;
                    }

                    // 2단계: 전체 메시지 읽기 (크기 필드 포함!)
                    byte[] fullMessage = new byte[totalMessageSize];

                    // 이미 읽은 4바이트(크기) 복사
                    lengthBuffer.CopyTo(fullMessage, 0);

                    // 나머지 읽기
                    int remainingBytes = totalMessageSize - 4;
                    int totalBytesRead = 0;

                    while (totalBytesRead < remainingBytes)
                    {
                        bytesRead = await m_stream.ReadAsync(
                            fullMessage,
                            4 + totalBytesRead,  // 크기(4) 이후부터
                            remainingBytes - totalBytesRead
                        );

                        if (bytesRead == 0)
                        {
                            LogWithTimestamp($"[Session {SessionId}] Connection lost while reading message");
                            break;
                        }
                        totalBytesRead += bytesRead;
                    }

                    if (totalBytesRead < remainingBytes)
                    {
                        LogWithTimestamp($"[Session {SessionId}] Incomplete message received");
                        break;
                    }

                    // 역직렬화 (전체 메시지 전달)
                    Protocol? protocol = Protocol.Deserialize(fullMessage);
                    if (protocol != null)
                    {
                        LogWithTimestamp($"[Session {SessionId}] Protocol Type: {protocol.Type}");
                        await HandleProtocol(protocol);
                        LogWithTimestamp($"[Session {SessionId}] Protocol handled, continuing loop");
                    }
                    else
                    {
                        LogWithTimestamp($"[Session {SessionId}] Protocol deserialization failed");
                    }
                }
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Session {SessionId}] ReceiveLoop error: {e.Message}");
            }

            LogWithTimestamp($"[Session {SessionId}] ReceiveLoop exited");
            Disconnect();
        }

        /// <summary>
        /// TCP 소켓 연결 상태 체크
        /// </summary>
        private bool IsSocketConnected()
        {
            try
            {
                if (TcpClient == null || TcpClient.Client == null)
                    return false;

                Socket socket = TcpClient.Client;

                // Poll을 사용하여 연결 상태 확인
                bool part1 = socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (socket.Available == 0);

                if (part1 && part2)
                    return false; // 연결 끊김
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 프로토콜 처리 - 핸들러에 위임
        /// </summary>
        private async Task HandleProtocol(Protocol _protocol)
        {
            await m_protocolHandler.HandleProtocol(_protocol);
        }

        /// <summary>
        /// 하트비트 처리
        /// </summary>
        private async Task HandleHeartbeat(Protocol _protocol)
        {
            UpdateLastActivity();

            // 하트비트 응답 전송
            Protocol ackProtocol = new Protocol(CommonLib.ProtocolType.HEARTBEAT_ACK)
                .AddParam("serverTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await SendAsync(ackProtocol.Serialize());
        }

        // REQUEST_REGISTER
        // ID: 10005
        // param: username (string), password (string)
        // 응답 param: 없음
        private async Task Handle_RequestRegister(Protocol protocol)
        {
            var username = protocol.GetParam<string>("username");
            var password = protocol.GetParam<string>("password");

            // 파라미터 검증
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                LogWithTimestamp($"[Session {SessionId}] Register Failed - Invalid parameters (username or password is empty)");
                var errorResponse = new Response(protocol.Type, StateCode.FAIL);
                errorResponse.AddParam("message", "Username and password are required");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            // 사용자명 길이 검증 (예: 3-20자)
            if (username.Length < 3 || username.Length > 20)
            {
                LogWithTimestamp($"[Session {SessionId}] Register Failed - Invalid username length (username: {username})");
                var errorResponse = new Response(protocol.Type, StateCode.FAIL);
                errorResponse.AddParam("message", "Username must be between 3 and 20 characters");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            // 비밀번호 길이 검증 (예: 4-50자)
            if (password.Length < 4 || password.Length > 50)
            {
                LogWithTimestamp($"[Session {SessionId}] Register Failed - Invalid password length");
                var errorResponse = new Response(protocol.Type, StateCode.FAIL);
                errorResponse.AddParam("message", "Password must be between 4 and 50 characters");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            // DB 접근 가능 여부 확인
            // 회원가입 시도
            bool registerSuccess = m_authDB.RegisterUser(username, password);
            if (!registerSuccess)
            {
                LogWithTimestamp($"[Session {SessionId}] Register Failed - User already exists or database error (username: {username})");
                var errorResponse = new Response(protocol.Type, StateCode.FAIL);
                errorResponse.AddParam("message", "Username already exists or registration failed");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            // 회원가입 성공
            LogWithTimestamp($"[Session {SessionId}] Register Success - UserName: {username}");

            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("message", "Registration successful. Please login.");
            await SendAsync(response.Serialize());
        }

        // REQUEST_REGISTER_AUTO
        // ID: 10006
        // param: 없음
        // 응답 param: username (string), password (string)
        private async Task Handle_RequestRegisterAuto(Protocol protocol)
        {
            LogWithTimestamp($"[Session {SessionId}] Auto Register Request");

            // 자동 username/password 생성 (중복 체크 포함)
            string username = string.Empty;
            string password = string.Empty;
            int maxRetries = 10;
            bool success = false;

            for (int i = 0; i < maxRetries; i++)
            {
                // Username 생성: guest_ + 12자리 랜덤 문자열
                username = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 12);

                // Password 생성: 16자리 랜덤 문자열
                password = Guid.NewGuid().ToString("N").Substring(0, 16);

                // 중복 체크
                if (!m_authDB.UserExists(username))
                {
                    // 회원가입 시도
                    if (m_authDB.RegisterUser(username, password))
                    {
                        success = true;
                        break;
                    }
                }

                LogWithTimestamp($"[Session {SessionId}] Auto Register - Username collision, retrying... ({i + 1}/{maxRetries})");
            }

            if (!success)
            {
                LogWithTimestamp($"[Session {SessionId}] Auto Register Failed - Could not generate unique username after {maxRetries} attempts");
                var errorResponse = new Response(protocol.Type, StateCode.SERVER_ERROR);
                errorResponse.AddParam("message", "Failed to generate account. Please try again.");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            // 회원가입 성공
            LogWithTimestamp($"[Session {SessionId}] Auto Register Success - UserName: {username}");

            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("username", username);
            response.AddParam("password", password);
            response.AddParam("message", "Auto registration successful. Please save your credentials.");
            await SendAsync(response.Serialize());
        }

        // REQUEST_LOGIN
        // ID: 10000
        // param: username (string), password (string)
        // 응답 param: sessionId (string)
        private async Task Handle_RequestLogin(Protocol protocol)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LogWithTimestamp($"━━━ [DB 직접 로그인 처리 시작] ━━━");

            var username = protocol.GetParam<string>("username");
            var password = protocol.GetParam<string>("password");

            // 파라미터 검증
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                LogWithTimestamp($"[Session {SessionId}] Login Failed - Invalid parameters (username or password is empty)");
                var errorResponse = new Response(protocol.Type, StateCode.AUTH_FAILURE);
                errorResponse.AddParam("message", "Username and password are required");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // DB 접근 가능 여부 확인
            // 로그인 처리
            UserInfo? user = m_authDB.AuthenticateUser(username, password);
            if (user == null)
            {
                LogWithTimestamp($"[Session {SessionId}] Login Failed - Invalid credentials (username: {username})");
                var errorResponse = new Response(protocol.Type, StateCode.AUTH_FAILURE);
                errorResponse.AddParam("message", "Invalid username or password");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // 인증 성공 - UserInfo 설정
            m_userInfo = user.Value;
            LogWithTimestamp($"[Session {SessionId}] Login Success - UserId: {m_userInfo.UserId}, UserName: {m_userInfo.UserName}");

            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("sessionId", SessionId);

            LogWithTimestamp($"━━━ [응답 전송] ━━━");
            LogWithTimestamp($"[Session {SessionId}] Response Type: {response.Type}");
            LogWithTimestamp($"[Session {SessionId}] protoId: {protocol.Type}");
            LogWithTimestamp($"[Session {SessionId}] Processing Time: {sw.ElapsedMilliseconds}ms");

            byte[] responseData = response.Serialize();
            LogWithTimestamp($"[Session {SessionId}] Response Size: {responseData.Length} bytes");

            await SendAsync(response.Serialize());
            LogWithTimestamp($"[Session {SessionId}] 전송 완료: {sw.ElapsedMilliseconds}ms");
        }

        // REQUEST_LOGIN_WITH_TOKEN
        // ID: 10007
        // param: token (string) - JWT Access Token
        // 응답 param: sessionId (string), userId (int), username (string)
        private async Task Handle_RequestLoginWithToken(Protocol protocol)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LogWithTimestamp($"━━━ [JWT 토큰 로그인 처리 시작] ━━━");

            var token = protocol.GetParam<string>("token");

            // 파라미터 검증
            if (string.IsNullOrEmpty(token))
            {
                LogWithTimestamp($"[Session {SessionId}] JWT Login Failed - Token is empty");
                var errorResponse = new Response(protocol.Type, StateCode.AUTH_FAILURE);
                errorResponse.AddParam("message", "Token is required");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // Auth 서버가 설정되지 않은 경우
            if (m_authServerClient == null)
            {
                LogWithTimestamp($"[Session {SessionId}] JWT Login Failed - Auth server client not configured");
                var errorResponse = new Response(protocol.Type, StateCode.SERVER_ERROR);
                errorResponse.AddParam("message", "JWT authentication is not available");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // Auth 서버에 토큰 검증 요청
            LogWithTimestamp($"[Session {SessionId}] Verifying token with Auth Server...");
            var verifyResult = await m_authServerClient.VerifyTokenAsync(token);

            if (!verifyResult.IsValid || string.IsNullOrEmpty(verifyResult.UserId))
            {
                LogWithTimestamp($"[Session {SessionId}] JWT Login Failed - {verifyResult.Message}");
                var errorResponse = new Response(protocol.Type, StateCode.AUTH_FAILURE);
                errorResponse.AddParam("message", verifyResult.Message);
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // 토큰 검증 성공 - UserId로 DB에서 사용자 정보 조회
            if (!int.TryParse(verifyResult.UserId, out int userId))
            {
                LogWithTimestamp($"[Session {SessionId}] JWT Login Failed - Invalid userId format: {verifyResult.UserId}");
                var errorResponse = new Response(protocol.Type, StateCode.SERVER_ERROR);
                errorResponse.AddParam("message", "Invalid user ID");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
                return;
            }

            // DB에서 사용자 정보 조회
            try
            {
                string query = "SELECT id, name FROM userinfo WHERE id = @id";
                var param = new MySql.Data.MySqlClient.MySqlParameter("@id", userId);
                using var reader = await m_authDB.ExecuteReaderAsync(query, param);

                if (!reader.Read())
                {
                    LogWithTimestamp($"[Session {SessionId}] JWT Login Failed - User not found in DB: {userId}");
                    var errorResponse = new Response(protocol.Type, StateCode.AUTH_FAILURE);
                    errorResponse.AddParam("message", "User not found");
                    await SendAsync(errorResponse.Serialize());
                    Disconnect();
                    return;
                }

                int dbUserId = reader.GetInt32("id");
                string username = reader.GetString("name");

                // 인증 성공 - UserInfo 설정
                m_userInfo = new UserInfo
                {
                    UserId = dbUserId,
                    UserName = username
                };

                LogWithTimestamp($"[Session {SessionId}] JWT Login Success - UserId: {m_userInfo.UserId}, UserName: {m_userInfo.UserName}");

                var response = new Response(protocol.Type, StateCode.SUCCESS);
                response.AddParam("sessionId", SessionId);
                response.AddParam("userId", m_userInfo.UserId);
                response.AddParam("username", m_userInfo.UserName);

                LogWithTimestamp($"━━━ [응답 전송] ━━━");
                LogWithTimestamp($"[Session {SessionId}] Processing Time: {sw.ElapsedMilliseconds}ms");

                await SendAsync(response.Serialize());
                LogWithTimestamp($"[Session {SessionId}] JWT 로그인 완료: {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogWithTimestamp($"[Session {SessionId}] JWT Login Error: {ex.Message}");
                var errorResponse = new Response(protocol.Type, StateCode.SERVER_ERROR);
                errorResponse.AddParam("message", "Database error");
                await SendAsync(errorResponse.Serialize());
                Disconnect();
            }
        }

        // REQUEST_LOGOUT
        // ID: 10001
        // param: 없음
        // 응답 param: 없음
        private async Task Handle_RequestLogout(Protocol protocol)
        {
            LogWithTimestamp($"[Session {SessionId}] Logout Request: UserName={m_userInfo.UserName}");

            // UserInfo 초기화
            m_userInfo = default;

            // 로그아웃 성공 응답
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            await SendAsync(response.Serialize());

            LogWithTimestamp($"[Session {SessionId}] Logout Success");

            // 연결 종료 (로그아웃 후 재로그인 필요)
            Disconnect();
        }

        // REQUEST_JOIN_LOBBY
        // ID: 10010
        // param: Page (int) - 0이면 전체
        // 응답 param: roomCount, page, roomList (RoomInfo[])
        private async Task Handle_RequestJoinLobby(Protocol protocol)
        {
            int page = protocol.GetParam<int>("Page");

            LogWithTimestamp($"[Session {SessionId}] Join Lobby Request - UserName: {m_userInfo.UserName}, Page: {page}");

            // RoomManager에서 방 리스트 가져오기
            var roomList = RoomManager.Instance.GetRoomList(page);
            int totalRoomCount = RoomManager.Instance.GetRoomList().Length;

            LogWithTimestamp($"[Session {SessionId}] Join Lobby Success - Total Rooms: {totalRoomCount}, Page: {page}, Rooms in Page: {roomList.Length}");

            // 응답 생성
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("roomCount", totalRoomCount);
            response.AddParam("page", page);
            response.AddObject("roomList", roomList);

            await SendAsync(response.Serialize());
        }

        // REFRESH_LOBBY
        // ID: 10011
        // param: 없음
        // 응답 param: roomList (RoomInfo[])
        private async Task Handle_RefreshLobby(Protocol protocol)
        {
            LogWithTimestamp($"[Session {SessionId}] Refresh Lobby Request - UserName: {m_userInfo.UserName}");

            // RoomManager에서 모든 방 리스트 가져오기
            var roomList = RoomManager.Instance.GetRoomList();

            LogWithTimestamp($"[Session {SessionId}] Refresh Lobby Success - Total Rooms: {roomList.Length}");

            // 응답 생성
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddObject("roomList", roomList);

            await SendAsync(response.Serialize());
        }

        // REQUEST_CREATE_ROOM
        // ID: 10012
        // param: roomName (string), mapId (int), isPrivate (bool)
        // 응답 param: roomId (string), slot (int)
        private async Task Handle_RequestCreateRoom(Protocol protocol)
        {
            LogWithTimestamp($"[Session {SessionId}] Create Room Request - UserName: {m_userInfo.UserName}");

            // 파라미터 불러오기
            string roomName = protocol.GetParam<string>("roomName");
            int mapId = protocol.GetParam<int>("mapId");
            bool isPrivate = protocol.GetParam<bool>("isPrivate");

            LogWithTimestamp($"[Session {SessionId}] Room Parameters - Name: {roomName}, MapID: {mapId}, Private: {isPrivate}");

            // 방 생성
            var room = RoomManager.Instance.CreateRoom();
            if (room == null)
            {
                LogWithTimestamp($"[Session {SessionId}] Create Room Failed - Room creation failed");
                var errorResponse = new Response(protocol.Type, StateCode.SERVER_ERROR);
                errorResponse.AddParam("message", "Failed to create room");
                await SendAsync(errorResponse.Serialize());
                return;
            }

            room.UpdateRoomInfo(roomName, mapId);

            LogWithTimestamp($"[Session {SessionId}] Create Room Success - RoomID: {room.RoomId}");

            // RoomManager에서 모든 방 리스트 가져오기
            var roomList = RoomManager.Instance.GetRoomList();

            // 성공 응답
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("roomId", room.RoomId);
            response.AddParam("slot", room.NextSlot());
            response.AddObject("roomList", roomList);

            await SendAsync(response.Serialize());
        }

        // REQUEST_JOIN_ROOM
        // ID: 10013
        // param: userId (int), roomId (string), slot (int)
        // 응답 param: roominfo (RoomInfo), chatChannelId (int)
        private async Task Handle_RequestJoinRoom(Protocol protocol)
        {
            LogWithTimestamp($"[Session {SessionId}] Join Room Request - UserName: {m_userInfo.UserName}");

            // 파라미터 불러오기
            int userId = protocol.GetParam<int>("userId");
            string roomId = protocol.GetParam<string>("roomId");
            int slot = protocol.GetParam<int>("slot");

            LogWithTimestamp($"[Session {SessionId}] Room Parameters - RoomID: {roomId}, Slot: {slot}");

            // 룸 존재 확인
            var room = RoomManager.Instance.GetRoom(roomId);
            if (room == null)
            {
                LogWithTimestamp($"[Session {SessionId}] Join Room Failed - Room not found: {roomId}");
                var error = new Response(protocol.Type, StateCode.NO_RESOURCE);
                error.AddParam("message", "Room not found");
                await SendAsync(error.Serialize());
                return;
            }

            // 슬롯 -1이면 자동으로 빈 슬롯 찾기
            if (slot == -1)
            {
                slot = room.FindEmptySlot();
                if (slot == -1)
                {
                    LogWithTimestamp($"[Session {SessionId}] Join Room Failed - No empty slot available");
                    var error = new Response(protocol.Type, StateCode.FAIL);
                    error.AddParam("message", "Room is full");
                    await SendAsync(error.Serialize());
                    return;
                }
                LogWithTimestamp($"[Session {SessionId}] Auto-assigned to slot: {slot}");
            }
            // 슬롯 범위 검증 (자동 할당이 아닌 경우)
            else if (slot >= Room.MaxPlayers || slot < 0)
            {
                LogWithTimestamp($"[Session {SessionId}] Join Room Failed - Invalid slot: {slot}");
                var error = new Response(protocol.Type, StateCode.FAIL);
                error.AddParam("message", "Invalid slot number");
                await SendAsync(error.Serialize());
                return;
            }

            // 룸 입장 시도
            if (!room.TryAddPlayer(this, slot))
            {
                LogWithTimestamp($"[Session {SessionId}] Join Room Failed - Cannot join room");
                var error = new Response(protocol.Type, StateCode.FAIL);
                error.AddParam("message", "Failed to join room (slot may be occupied)");
                await SendAsync(error.Serialize());
                return;
            }

            LogWithTimestamp($"[Session {SessionId}] Join Room Success - RoomID: {roomId}, Slot: {slot}");

            var temp = room.GetRoomInfo();
            // 성공 응답
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            response.AddParam("chatChannelId", room.ChatChID);
            response.AddParam("roomId", temp.roomId);
            response.AddStruct<RoomInfo>("roomInfo", temp.roomInfo);
            response.AddObject("users", temp.waitusers);
            await SendAsync(response.Serialize());
        }

        /// <summary>
        /// 에러 메시지 전송
        /// </summary>
        private async Task SendErrorAsync(string _errorMessage)
        {
            Protocol protocol = new Protocol(CommonLib.ProtocolType.RESPONSE)
                .AddParam("result", (byte)ServerMessage.Error)
                .AddParam("message", _errorMessage);

            await SendAsync(protocol.Serialize());
        }

        /// <summary>
        /// 데이터 전송 (스레드 세이프)
        /// </summary>
        public async Task SendAsync(byte[] _data)
        {
            if (!m_isConnected || m_stream == null)
                return;

            try
            {
                lock (m_sendLock)
                {
                    m_stream.Write(_data, 0, _data.Length);
                    m_stream.Flush();
                }

                UpdateLastActivity(); // 전송 시에도 활동 시간 갱신
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Session {SessionId}] Send error: {e.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// 연결 종료
        /// </summary>
        public void Disconnect()
        {
            if (!m_isConnected)
                return;

            m_isConnected = false;
            LogWithTimestamp($"[Session {SessionId}] Disconnecting...");

            try
            {
                m_timeoutCheckTimer?.Dispose();
                m_stream?.Close();
                TcpClient?.Close();
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Session {SessionId}] Error during disconnect: {e.Message}");
            }
        }

        /// <summary>
        /// 정리 작업
        /// </summary>
        private void Cleanup()  
        {
            // 룸에서 제거
            if (CurrentRoom != null)
            {
                CurrentRoom.TryRemovePlayer(this);
                CurrentRoom = null;
            }

            Disconnect();
            LogWithTimestamp($"[Session {SessionId}] Cleaned up");
        }
    }
}