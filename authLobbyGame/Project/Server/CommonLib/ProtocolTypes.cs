namespace CommonLib
{
    /// <summary>
    /// 프로토콜 타입 정의
    /// </summary>
    public static class ProtocolType
    {
        // 클라이언트 -> 서버 (서버 공통 타입과 정확히 일치)
        public const int REQUEST_LOGIN = 10000;                 // 로그인 요청 (DB 직접 인증)
        public const int REQUEST_LOGOUT = 10001;                //로그아웃 요청
        public const int CHAT_MESSAGE = 10002;                  //메세지 전송
        public const int HEARTBEAT = 10003;                     // 하트비트 (연결 유지 확인)
        public const int REQUEST_TABLEDATA = 10004;             // 테이블 데이터 요청
        public const int REQUEST_REGISTER = 10005;              // 회원가입 요청
        public const int REQUEST_REGISTER_AUTO = 10006;         // 자동 회원가입 요청 (게스트)
        public const int REQUEST_LOGIN_WITH_TOKEN = 10007;      // JWT 토큰 기반 로그인 요청

        public const int CHAT_CHANNEL_JOIN = 10100;
        public const int CHAT_CHANNEL_REFRESH = 10101;
        public const int CHAT_CHANNEL_LEFT = 10102;

        public const int REQUEST_JOIN_LOBBY = 10010;                //로비 접속 요청

        public const int REFRESH_LOBBY = 10011;                     //로비 새로고침 요청
        public const int REQUEST_CREATE_ROOM = 10012;               //방 생성 요청
        public const int REQUEST_JOIN_ROOM = 10013;                 //방 입장 요청
        public const int REQUEST_READY = 10014;                     // 게임 레디
        public const int REQUEST_LEFT_ROOM = 10015;                 // 방 퇴장 요청
        public const int REQUEST_REFRESH_JOINED_ROOM_INFO = 10016;  // 입장한 방 정보 갱신 요청

        public const int REQUEST_GAME_CL_READY = 10200;

        // 서버 -> 클라이언트
        public const int RESPONSE = 20000;                      // 전체 공통 응답처리
        public const int BRODCAST_SYSTEM = 20001;               // 시스템 공통 알림
        public const int BRODCAST_CHAT_MESSAGE = 20002;         // 메시지 브로드캐스트
        public const int HEARTBEAT_ACK = 20003;                 // 하트비트 응답

        public const int USER_JOINED = 20010;                   // 유저 접속 알림
        public const int USER_LEFT = 20011;                     // 유저 이탈 알림
        public const int ROOM_INFO_CHANGED = 20012;             // 방 정보 변경 알림
        public const int ROOM_CLOSED = 20013;                   // 방 삭제 알림

        // 게임 브로드캐스트 (인게임 이벤트)
        public const int GAME_SET = 20200;
        public const int GAME_STARTED = 20201;                   // 게임 시작 알림
        public const int GAME_STATE = 20202;                     // 게임 상태 전달. 매 틱마다 전송.
        public const int GAME_ENDED = 20026;                     // 게임 종료 알림

        public const int SUBMIT_COMMAND = 30100;
    }

    /// <summary>
    /// 채팅 메시지 데이터 구조체
    /// </summary>
    public struct ChatMessage
    {
        public string SenderId { get; set; }
        public string Message { get; set; }
        public long Timestamp { get; set; }
        public int MessageType { get; set; } // 0: LOBBY, 1: INGAME

        public override string ToString()
        {
            return $"[{SenderId}]: {Message}";
        }
    }

    /// <summary>
    /// 룸 정보 구조체
    /// </summary>
    public struct RoomInfo
    {
        public string RoomId { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string RoomName { get; set; }
        public RoomState RoomState { get; set; }
        public int MapID { get; set; }

        public override string ToString()
        {
            return $"Room {RoomId}: {PlayerCount}/{MaxPlayers}";
        }
    }


    public struct UserInfo
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
    }

    public struct WaittingRoomUser
    {
        public UserInfo userInfo { get; set; }
        public bool IsReady { get; set; }
    }
}