using System.Collections.Generic;

namespace CommonServer.Shared
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Username { get; set; } = "";
        public string Message { get; set; } = "";
        public List<ServerInfo> Servers { get; set; } = new List<ServerInfo>();
    }
}