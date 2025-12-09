namespace AuthServer.Models
{
    /// <summary>
    /// 게임 서버 정보
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// 서버 이름
        /// </summary>
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// 서버 타입 (Lobby, Game 등)
        /// </summary>
        public string ServerType { get; set; } = string.Empty;

        /// <summary>
        /// 호스트 주소
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// TCP 포트
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// UDP 포트 (게임 서버용, 선택)
        /// </summary>
        public int UdpPort { get; set; }

        /// <summary>
        /// 현재 접속 인원
        /// </summary>
        public int CurrentPlayers { get; set; }

        /// <summary>
        /// 최대 수용 인원
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// 서버 상태 (Online, Full, Maintenance 등)
        /// </summary>
        public string Status { get; set; } = "Online";

        /// <summary>
        /// 마지막 하트비트 시간
        /// </summary>
        public DateTime LastHeartbeat { get; set; }
    }

    /// <summary>
    /// 서버 등록 요청
    /// </summary>
    public class RegisterServerRequest
    {
        public string ServerName { get; set; } = string.Empty;
        public string ServerType { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public int UdpPort { get; set; }
        public int MaxCapacity { get; set; }
        public string SecretKey { get; set; } = string.Empty; // 서버 인증용
    }

    /// <summary>
    /// 서버 하트비트 요청
    /// </summary>
    public class ServerHeartbeatRequest
    {
        public string ServerName { get; set; } = string.Empty;
        public int CurrentPlayers { get; set; }
        public string Status { get; set; } = "Online";
        public string SecretKey { get; set; } = string.Empty;
    }
}
