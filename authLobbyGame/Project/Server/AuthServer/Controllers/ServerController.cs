using Microsoft.AspNetCore.Mvc;
using AuthServer.Models;
using AuthServer.Services;

namespace AuthServer.Controllers
{
    /// <summary>
    /// 게임 서버 등록 및 관리 컨트롤러
    /// Lobby/Game 서버들이 자신을 등록하고 하트비트를 전송
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ServerController : ControllerBase
    {
        private readonly ServerRegistryService _serverRegistry;

        public ServerController(ServerRegistryService serverRegistry)
        {
            _serverRegistry = serverRegistry;
        }

        /// <summary>
        /// 서버 등록
        /// Lobby/Game 서버가 시작 시 자신을 등록
        /// </summary>
        [HttpPost("register")]
        public IActionResult RegisterServer([FromBody] RegisterServerRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ServerName))
            {
                return BadRequest(new { success = false, message = "Invalid request" });
            }

            bool success = _serverRegistry.RegisterServer(request);

            if (success)
            {
                return Ok(new { success = true, message = "Server registered successfully" });
            }

            return Unauthorized(new { success = false, message = "Invalid credentials" });
        }

        /// <summary>
        /// 서버 하트비트
        /// 서버가 주기적으로 자신의 상태를 전송 (현재 인원, 상태 등)
        /// </summary>
        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] ServerHeartbeatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ServerName))
            {
                return BadRequest(new { success = false });
            }

            bool success = _serverRegistry.UpdateHeartbeat(request);

            if (success)
            {
                return Ok(new { success = true });
            }

            return NotFound(new { success = false, message = "Server not found" });
        }

        /// <summary>
        /// 서버 등록 해제
        /// 서버 종료 시 호출
        /// </summary>
        [HttpPost("unregister")]
        public IActionResult UnregisterServer([FromBody] UnregisterServerRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ServerName))
            {
                return BadRequest(new { success = false });
            }

            bool success = _serverRegistry.UnregisterServer(request.ServerName, request.SecretKey);

            if (success)
            {
                return Ok(new { success = true });
            }

            return Unauthorized(new { success = false });
        }

        /// <summary>
        /// 사용 가능한 Lobby 서버 목록 조회
        /// 클라이언트가 로그인 후 호출
        /// </summary>
        [HttpGet("lobby-list")]
        public IActionResult GetLobbyServers()
        {
            var lobbyServers = _serverRegistry.GetServersByType("Lobby");

            return Ok(new
            {
                success = true,
                servers = lobbyServers.Select(s => new
                {
                    serverName = s.ServerName,
                    host = s.Host,
                    port = s.Port,
                    currentPlayers = s.CurrentPlayers,
                    maxCapacity = s.MaxCapacity,
                    status = s.Status
                })
            });
        }

        /// <summary>
        /// 추천 Lobby 서버 조회
        /// 클라이언트가 자동 연결할 서버를 요청
        /// </summary>
        [HttpGet("recommended-lobby")]
        public IActionResult GetRecommendedLobby()
        {
            var server = _serverRegistry.GetRecommendedLobbyServer();

            if (server == null)
            {
                return NotFound(new { success = false, message = "No lobby servers available" });
            }

            return Ok(new
            {
                success = true,
                server = new
                {
                    serverName = server.ServerName,
                    host = server.Host,
                    port = server.Port,
                    currentPlayers = server.CurrentPlayers,
                    maxCapacity = server.MaxCapacity,
                    status = server.Status
                }
            });
        }

        /// <summary>
        /// 모든 활성 서버 조회 (관리자용)
        /// </summary>
        [HttpGet("all")]
        public IActionResult GetAllServers()
        {
            var servers = _serverRegistry.GetAllActiveServers();

            return Ok(new
            {
                success = true,
                count = servers.Count,
                servers = servers.Select(s => new
                {
                    serverName = s.ServerName,
                    serverType = s.ServerType,
                    host = s.Host,
                    port = s.Port,
                    udpPort = s.UdpPort,
                    currentPlayers = s.CurrentPlayers,
                    maxCapacity = s.MaxCapacity,
                    status = s.Status,
                    lastHeartbeat = s.LastHeartbeat
                })
            });
        }
    }

    /// <summary>
    /// 서버 등록 해제 요청
    /// </summary>
    public class UnregisterServerRequest
    {
        public string ServerName { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
    }
}
