using CommonLib;
using CommonLib.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LobbyServer.Core.Game.Session;

namespace LobbyServer.Core.Game.Entities
{
    /// <summary>
    /// 범용 룸 클래스 - 멀티플레이어 룸 관리
    /// </summary>
    public class Room : ICommandSender
    {
        #region 상수
        public const int MaxPlayers = 2;
        #endregion

        #region 필드
        private readonly object m_lockObj = new object();
        private RoomUser[] m_users = new RoomUser[MaxPlayers];
        #endregion

        #region 기본 속성
        public string RoomId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public RoomState State { get; private set; }
        public int MapID { get; private set; }
        public int ChatChID => -1; // 채팅 채널 ID (추후 구현)
        #endregion

        #region 계산된 속성
        public RoomInfo RoomInfo
        {
            get
            {
                lock (m_lockObj)
                {
                    return new RoomInfo()
                    {
                        RoomId = RoomId,
                        RoomName = Name,
                        RoomState = State,
                        MaxPlayers = MaxPlayers,
                        PlayerCount = PlayerCount,
                        MapID = MapID,
                    };
                }
            }
        }

        public int PlayerCount
        {
            get
            {
                lock (m_lockObj)
                {
                    return m_users.Count(user => user != null && user.IsValid);
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (m_lockObj)
                {
                    return m_users.Count(user => user != null && user.IsValid) >= MaxPlayers;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (m_lockObj)
                {
                    return m_users.All(user => user == null || !user.IsValid);
                }
            }
        }
        #endregion

        #region 생성자
        public Room(string roomId, int mapID = 0)
        {
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            MapID = mapID;
            State = RoomState.Open;

            for (int i = 0; i < MaxPlayers; i++)
            {
                m_users[i] = new RoomUser();
                m_users[i].OnChangedReady += HandleOnReady;
            }

            LogWithTimestamp($"[Room {RoomId}] Created with MapID: {MapID}");
        }


        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }
        #endregion

        #region 룸 관리 메소드
        public int NextSlot()
        {
            lock (m_lockObj)
            {
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (m_users[i] == null || !m_users[i].IsValid)
                        return i;
                }
                return -1;
            }
        }

        /// <summary>
        /// 모든 플레이어가 준비 완료되었을 때 호출됩니다.
        /// 게임 특화 로직은 이 메서드를 오버라이드하여 구현하세요.
        /// </summary>
        protected virtual void HandleOnReady()
        {
            lock (m_lockObj)
            {
                // 모든 플레이어가 유효하고 준비 상태인지 확인
                bool allPlayersReady = m_users.All(user =>
                    user != null && user.IsValid && user.IsReady);

                LogWithTimestamp($"[Room {RoomId}] players ready changed.IsAllReady = {allPlayersReady}");
                if (!allPlayersReady)
                    return;

                // 게임 시작 로직은 하위 클래스에서 구현
                LogWithTimestamp($"[Room {RoomId}] All players ready.");
                OnAllPlayersReady();
            }
        }

        /// <summary>
        /// 모든 플레이어가 준비되었을 때 호출되는 가상 메서드
        /// 게임 특화 로직을 구현하려면 이 메서드를 오버라이드하세요.
        /// </summary>
        protected virtual void OnAllPlayersReady()
        {
            // 기본 구현: 아무것도 하지 않음
            // 게임 서버에서 이 메서드를 오버라이드하여 게임 시작 로직 구현
        }

        public void UpdateRoomInfo(string name = "", int mapId = -1)
        {
            lock (m_lockObj)
            {
                if (!string.IsNullOrEmpty(name))
                    Name = name;

                if (mapId >= 0)
                    MapID = mapId;

                var value = GetRoomInfo();


                Protocol proto = new Protocol(ProtocolType.ROOM_INFO_CHANGED)
                    .AddParam("roomId", value.roomId)
                    .AddStruct("roomInfo", value.roomInfo)
                    .AddObject<WaittingRoomUser[]>("users", value.waitusers);

                Broadcast(proto);
            }

            LogWithTimestamp($"[Room {RoomId}] Info updated - Name: {Name}, MapID: {MapID}");
        }

        public (string roomId, RoomInfo roomInfo, WaittingRoomUser[] waitusers) GetRoomInfo()
        {
            WaittingRoomUser[] tempUsers = new WaittingRoomUser[m_users.Length];

            for (int index = 0; index < tempUsers.Length; index++)
            {
                var temp = new WaittingRoomUser();
                temp.userInfo = new UserInfo()
                {
                    UserId = m_users[index].ID,
                    UserName = m_users[index].Name
                };
                tempUsers[index] = temp;
            }

            return (roomid: RoomId, roomInfo: RoomInfo, waitusers: tempUsers);

        }

        public async Task NotifyRoomClosed()
        {
            List<RoomUser> validUsers = new List<RoomUser>();

            lock (m_lockObj)
            {
                foreach (var user in m_users)
                {
                    if (user != null && user.IsValid)
                    {
                        validUsers.Add(user);
                    }
                }
            }

            if (validUsers.Count == 0)
                return;

            Protocol protocol = new Protocol(ProtocolType.ROOM_CLOSED)
                .AddParam("roomId", RoomId)
                .AddParam("reason", "Room has been closed");

            byte[] data = protocol.Serialize();

            var tasks = validUsers.Select(user =>
                SafeSendAsync(user, data, "room closure notification"));

            await Task.WhenAll(tasks);

            LogWithTimestamp($"[Room {RoomId}] Closed notification sent to {validUsers.Count} players");
        }

        public virtual void Dispose()
        {
            lock (m_lockObj)
            {
                // 이벤트 구독 해제
                for (int i = 0; i < m_users.Length; i++)
                {
                    if (m_users[i] != null)
                    {
                        m_users[i].OnChangedReady -= HandleOnReady;
                        m_users[i].Cleanup();
                        m_users[i] = null;
                    }
                }
            }

            LogWithTimestamp($"[Room {RoomId}] Disposed");
        }
        #endregion

        #region 플레이어 관리 메소드
        /// <summary>
        /// 빈 슬롯을 찾아서 반환. 빈 슬롯이 없으면 -1 반환
        /// </summary>
        public int FindEmptySlot()
        {
            lock (m_lockObj)
            {
                for (int i = 0; i < m_users.Length; i++)
                {
                    if (m_users[i] == null || !m_users[i].IsValid)
                    {
                        return i;
                    }
                }
                return -1; // 빈 슬롯 없음
            }
        }

        public bool TryAddPlayer(ClientSession session, int slot)
        {
            if (session == null)
            {
                LogWithTimestamp($"[Room {RoomId}] Cannot add null session");
                return false;
            }

            lock (m_lockObj)
            {
                if (slot < 0 || slot >= MaxPlayers)
                {
                    LogWithTimestamp($"[Room {RoomId}] Invalid slot: {slot}");
                    return false;
                }

                if (m_users[slot] == null || !m_users[slot].IsValid)
                {
                    if (m_users[slot] == null)
                        m_users[slot] = new RoomUser();

                    m_users[slot].Initialize(session);
                    session.CurrentRoom = this;

                    LogWithTimestamp($"[Room {RoomId}] Player {session.SessionId} joined at slot {slot}. ({PlayerCount}/{MaxPlayers})");

                    // 다른 플레이어에게 입장 알림
                    BroadcastUserJoined(m_users[slot]);

                    return true;
                }

                LogWithTimestamp($"[Room {RoomId}] Slot {slot} is already occupied");
                return false;
            }
        }

        public bool TryRemovePlayer(ClientSession session)
        {
            if (session == null)
                return false;

            lock (m_lockObj)
            {
                for (int i = 0; i < m_users.Length; i++)
                {
                    if (m_users[i] != null && m_users[i].IsSameSession(session))
                    {
                        return RemovePlayer(i);
                    }
                }
            }

            return false;
        }

        public bool RemovePlayer(int slot)
        {
            lock (m_lockObj)
            {
                if (slot < 0 || slot >= m_users.Length)
                    return false;

                if (m_users[slot] == null || !m_users[slot].IsValid)
                    return false;

                int playerId = m_users[slot].ID;
                string playerName = m_users[slot].Name;

                m_users[slot].Cleanup();

                LogWithTimestamp($"[Room {RoomId}] Player {playerId} ({playerName}) left. ({PlayerCount}/{MaxPlayers})");

                // 다른 플레이어에게 퇴장 알림
                BroadcastUserLeft(playerId, playerName);

                return true;
            }
        }

        public void CloseAllConnections()
        {
            lock (m_lockObj)
            {
                int disconnectedCount = 0;

                foreach (var user in m_users)
                {
                    if (user != null && user.IsValid)
                    {
                        user.ForceDisconnect();
                        disconnectedCount++;
                    }
                }

                Array.Clear(m_users, 0, m_users.Length);

                LogWithTimestamp($"[Room {RoomId}] Closed all connections ({disconnectedCount} players)");
            }
        }
        #endregion

        #region 브로드캐스트 메소드
        public async Task BroadcastMessage(ClientSession sender, string message)
        {
            if (sender == null || string.IsNullOrEmpty(message))
                return;

            List<ClientSession> validSessions = new List<ClientSession>();

            lock (m_lockObj)
            {
                foreach (var user in m_users)
                {
                    if (user != null && user.IsValid && user.Session != null)
                    {
                        validSessions.Add(user.Session);
                    }
                }
            }

            if (validSessions.Count == 0)
                return;

            ChatMessage chatMsg = new ChatMessage
            {
                SenderId = sender.SessionId,
                Message = message,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MessageType = 0 // 0: ROOM_CHAT
            };

            Protocol protocol = new Protocol(ProtocolType.BRODCAST_CHAT_MESSAGE)
                .AddStruct("chatMessage", chatMsg);

            byte[] data = protocol.Serialize();

            var tasks = validSessions.Select(session =>
                SafeSendAsync(session, data, "chat message"));

            await Task.WhenAll(tasks);
        }

        private void Broadcast(Protocol proto)
        {
            if (proto == null)
                return;

            byte[] data = proto.Serialize();

            lock (m_lockObj)
            {
                foreach (var user in m_users)
                {
                    if (user != null && user.IsValid)
                    {
                        _ = user.HandleBrodcast(data);
                    }
                }
            }
        }

        private void BroadcastUserJoined(RoomUser joinedUser)
        {
            if (joinedUser == null)
                return;

            Protocol protocol = new Protocol(ProtocolType.USER_JOINED)
                .AddParam("userId", joinedUser.ID)
                .AddParam("userName", joinedUser.Name)
                .AddParam("playerCount", PlayerCount);

            Broadcast(protocol);
        }

        private void BroadcastUserLeft(int userId, string userName)
        {
            Protocol protocol = new Protocol(ProtocolType.USER_LEFT)
                .AddParam("userId", userId)
                .AddParam("userName", userName)
                .AddParam("playerCount", PlayerCount);

            Broadcast(protocol);
        }

        /// <summary>
        /// 안전한 비동기 전송 (예외 처리 포함)
        /// </summary>
        private async Task SafeSendAsync(dynamic target, byte[] data, string messageType)
        {
            try
            {
                if (target is ClientSession session)
                {
                    await session.SendAsync(data);
                }
                else if (target is RoomUser user)
                {
                    await user.HandleBrodcast(data);
                }
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Room {RoomId}] Failed to send {messageType}: {e.Message}");
            }
        }

        /// <summary>
        /// 게임으로 명령을 전송합니다. 현재는 인터페이스 구현용 기본 동작(로깅)만 수행합니다.
        /// 향후 실제 게임 서버로 전달하는 로직으로 교체해야 합니다.
        /// </summary>
        public async Task SendCommandToGame(Command command)
        {
            if (command == null)
                return;

            try
            {
                LogWithTimestamp($"[Room {RoomId}] SendCommandToGame invoked. Type={command.Type}, PlayerId={command.PlayerId}");
                // TODO: 실제 게임 서버로 라우팅하는 로직을 구현
            }
            catch (Exception e)
            {
                LogWithTimestamp($"[Room {RoomId}] SendCommandToGame error: {e.Message}");
            }

            await Task.CompletedTask;
        }
        #endregion
    }
}
