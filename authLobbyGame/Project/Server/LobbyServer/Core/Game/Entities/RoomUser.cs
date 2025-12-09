using CommonLib;
using ProtocolType = CommonLib.ProtocolType;
using LobbyServer.Core.Game.Session;
using LobbyServer.Core.Game.Managers;

namespace LobbyServer.Core.Game.Entities
{
    public class RoomUser
    {
        #region 상수
        const int DEFAULT_ID = -1;
        const string DEFAULT_NAME = "INVALID";
        #endregion

        #region 필드
        protected ClientSession? m_session;
        protected int m_id = DEFAULT_ID;        // 유저 식별자
        protected string m_name = DEFAULT_NAME; // 유저 닉네임
        protected bool m_IsReady = false;

        public System.Action OnChangedReady;
        #endregion

        #region 속성
        public int ID => m_id;
        public string Name => m_name;
        public bool IsReady => m_IsReady;
        public ClientSession Session => m_session;

        public bool IsValid
        {
            get
            {
                return m_session != null
                    && m_id != DEFAULT_ID
                    && m_name != DEFAULT_NAME;
            }
        }
        #endregion

        #region 초기화 및 리소스 관리
        /// <summary>
        /// 세션 탑재
        /// </summary>
        /// <param name="session"></param>
        public void Initialize(ClientSession session)
        {
            if (m_session != null)
                Cleanup();
            this.m_session = session;
            m_id = session.UserInfo.UserId;
            m_name = session.UserInfo.UserName;

            RegistorProtos();
        }

        /// <summary>
        /// 세션 빼기
        /// </summary>
        public void Cleanup()
        {
            this.m_session = null;
            m_id = DEFAULT_ID;
            m_name = DEFAULT_NAME;

            UnRegistorProtos();
        }

        public void ForceDisconnect()
        {
            if (m_session != null)
                m_session.Disconnect();
            Cleanup();
        }
        #endregion

        #region 유틸리티 메소드
        public bool IsSameSession(ClientSession session)
        {
            if (m_session == null)
                return false;
            return m_session.Equals(session);
        }

        public async Task HandleBrodcast(byte[] data)
        {
            if (m_session == null)
                return;

            await m_session.SendAsync(data);
        }
        #endregion

        #region 프로토콜 등록 관리
        protected virtual void RegistorProtos()
        {
            if (m_session == null)
                return;
            // Note: REQUEST_CREATE_ROOM and REQUEST_JOIN_ROOM are handled in ClientSession
            // Only register room-specific handlers here
            m_session.RegisterProto(ProtocolType.REQUEST_LEFT_ROOM, Handle_RequestLeftRoom);
            m_session.RegisterProto(ProtocolType.REQUEST_READY, Handle_RequestReady);
        }

        protected virtual void UnRegistorProtos()
        {
            if (m_session == null)
                return;
            // Note: REQUEST_CREATE_ROOM and REQUEST_JOIN_ROOM are handled in ClientSession
            // Only unregister room-specific handlers here
            m_session.UnRegisterProto(ProtocolType.REQUEST_LEFT_ROOM);
            m_session.UnRegisterProto(ProtocolType.REQUEST_READY);
        }
        #endregion

        #region 프로토콜 핸들러
        private async Task Handle_RequestReady(Protocol protocol)
        {
            if (m_session == null)
                return;

            var state = protocol.GetParam<bool>("isReady");
            m_IsReady = state;

            var response = new Response(protocol.Type, StateCode.SUCCESS);
            await m_session.SendAsync(response.Serialize());
            // 이벤트 호출
            OnChangedReady?.Invoke();
            return;
        }

        private async Task Handle_RequestLeftRoom(Protocol protocol)
        {
            if (m_session == null)
                return;

            if (m_session.CurrentRoom == null)
            {
                var error = new Response(protocol.Type, StateCode.FAIL, "입장한 룸 없음");

                await m_session.SendAsync(error.Serialize());
                return;
            }

            // 먼저 성공 응답을 보낸 후 방에서 제거
            // (TryRemovePlayer 내부에서 Cleanup()이 호출되어 m_session이 null이 됨)
            var response = new Response(protocol.Type, StateCode.SUCCESS);
            await m_session.SendAsync(response.Serialize());

            // 응답 전송 후 방에서 제거
            if (!m_session.CurrentRoom.TryRemovePlayer(m_session))
            {
                LogWithTimestamp($"[RoomUser] 방 퇴장 처리 중 오류 발생 (User: {m_id})");
            }

            return;
        }

        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }
        #endregion
    }
}
