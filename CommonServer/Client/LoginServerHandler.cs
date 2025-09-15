using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 로그인 서버와의 통신을 처리하는 클라이언트 측 핸들러
/// 커넥션 체크 → 로그인 요청 → 서버 목록 수신 처리
/// </summary>
public class LoginServerHandler : BaseServerHandler
{
    private bool _connectionEstablished = false;
    private LoginResult _loginResult = null;
    private readonly TaskCompletionSource<bool> _connectionTcs = new();
    private readonly TaskCompletionSource<LoginResult> _loginResultTcs = new();

    protected override async Task HandleProtocolAsync(Protocol protocol)
    {
        switch (protocol.ID)
        {
            case Protocol.IDs.USER_ENTER_SUCCESS:
                await HandleUserEnterSuccessAsync(protocol);
                break;
            case Protocol.IDs.USER_LOGIN_SUCCESS:
                await HandleLoginSuccessAsync(protocol);
                break;
            case Protocol.IDs.USER_LOGIN_FAIL:
                await HandleLoginFailAsync(protocol);
                break;
            case Protocol.IDs.PONG_RESPONSE:
                Console.WriteLine($"[LOGIN] Pong 수신: {protocol.Parameter.GetValueOrDefault(0, "")}");
                break;
            default:
                Console.WriteLine($"[LOGIN] 알 수 없는 프로토콜 ID: {protocol.ID}");
                break;
        }
    }

    /// <summary>
    /// USER_ENTER_SUCCESS 응답 처리 - 커넥션 체크 완료
    /// </summary>
    private async Task HandleUserEnterSuccessAsync(Protocol protocol)
    {
        Console.WriteLine("[LOGIN] 서버 접속 확인 완료 (USER_ENTER_SUCCESS)");

        var message = protocol.Parameter.GetValueOrDefault(0, "접속 성공").ToString();
        Console.WriteLine($"[LOGIN] 서버 메시지: {message}");

        _connectionEstablished = true;
        _connectionTcs.TrySetResult(true);
    }

    /// <summary>
    /// 로그인 성공 응답 처리 - 서버 목록 파싱
    /// </summary>
    private async Task HandleLoginSuccessAsync(Protocol protocol)
    {
        Console.WriteLine("[LOGIN] 로그인 성공 응답 수신 (USER_LOGIN_SUCCESS)");

        var result = new LoginResult
        {
            Success = true,
            Username = protocol.Parameter.GetValueOrDefault(0, "").ToString(),
            Message = protocol.Parameter.GetValueOrDefault(1, "로그인 성공").ToString(),
            Servers = new List<ServerInfo>()
        };

        Console.WriteLine($"[LOGIN] 사용자: {result.Username}");
        Console.WriteLine($"[LOGIN] 메시지: {result.Message}");

        // 서버 목록 파싱 (파라미터 3부터 서버 정보들)
        // 형식: 서버타입:주소 (예: GAME:127.0.0.1:5001, CHAT:127.0.0.1:5002)
        int paramIndex = 3;
        while (protocol.Parameter.ContainsKey(paramIndex))
        {
            var serverData = protocol.Parameter[paramIndex].ToString();
            var serverInfo = ParseServerInfo(serverData);

            if (serverInfo != null)
            {
                result.Servers.Add(serverInfo);
                Console.WriteLine($"[LOGIN] 서버 발견: {serverInfo.Type} - {serverInfo.Address}");
            }

            paramIndex++;
        }

        Console.WriteLine($"[LOGIN] 총 {result.Servers.Count}개 서버 발견");

        _loginResult = result;
        _loginResultTcs.TrySetResult(result);
    }

    /// <summary>
    /// 서버 정보 문자열 파싱
    /// </summary>
    private ServerInfo ParseServerInfo(string serverData)
    {
        try
        {
            var parts = serverData.Split(':');
            if (parts.Length >= 3) // TYPE:IP:PORT
            {
                var serverType = parts[0].ToUpper();
                var address = string.Join(":", parts, 1, parts.Length - 1); // IP:PORT

                return new ServerInfo
                {
                    Type = serverType,
                    Address = address
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOGIN] 서버 정보 파싱 오류: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 로그인 실패 응답 처리
    /// </summary>
    private async Task HandleLoginFailAsync(Protocol protocol)
    {
        Console.WriteLine("[LOGIN] 로그인 실패 응답 수신 (USER_LOGIN_FAIL)");

        var errorMessage = protocol.Parameter.GetValueOrDefault(0, "로그인 실패").ToString();
        Console.WriteLine($"[LOGIN] 실패 사유: {errorMessage}");

        var result = new LoginResult
        {
            Success = false,
            Username = "",
            Message = errorMessage,
            Servers = new List<ServerInfo>()
        };

        _loginResult = result;
        _loginResultTcs.TrySetResult(result);
    }

    /// <summary>
    /// 커넥션 체크 대기
    /// </summary>
    public async Task<bool> WaitForConnection(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = await _connectionTcs.Task.WaitAsync(cts.Token);
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[LOGIN] 커넥션 체크 대기 시간 초과");
            return false;
        }
    }

    /// <summary>
    /// 로그인 요청 전송
    /// </summary>
    public void SendLoginRequest(string username, string password)
    {
        if (!_connectionEstablished)
        {
            Console.WriteLine("[LOGIN] 경고: 커넥션이 확인되지 않은 상태에서 로그인 요청");
        }

        var loginProtocol = new Protocol(Protocol.IDs.USER_LOGIN_REQUEST, Protocol.ProtocolType.ToServer);
        loginProtocol.SetParam(0, username);
        loginProtocol.SetParam(1, password);
        SendMessage(loginProtocol);
        Console.WriteLine($"[LOGIN] 로그인 요청 전송 (USER_LOGIN_REQUEST): {username}");
    }

    /// <summary>
    /// 로그인 결과 대기
    /// </summary>
    public async Task<LoginResult> WaitForLoginResult(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = await _loginResultTcs.Task.WaitAsync(cts.Token);
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[LOGIN] 로그인 응답 대기 시간 초과");
            return new LoginResult { Success = false, Message = "응답 시간 초과", Servers = new List<ServerInfo>() };
        }
    }

    protected override Task OnConnectedAsync()
    {
        Console.WriteLine("[LOGIN] 로그인 서버에 연결되었습니다. USER_ENTER_SUCCESS 대기 중...");
        return Task.CompletedTask;
    }

    protected override Task OnDisconnectedAsync()
    {
        Console.WriteLine("[LOGIN] 로그인 서버에서 연결 해제되었습니다.");
        return Task.CompletedTask;
    }
}

/// <summary>
/// 서버 정보를 담는 클래스
/// </summary>
public class ServerInfo
{
    public string Type { get; set; } = ""; // GAME, CHAT, etc.
    public string Address { get; set; } = ""; // IP:PORT
}

/// <summary>
/// 로그인 결과를 담는 클래스
/// </summary>
public class LoginResult
{
    public bool Success { get; set; }
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public List<ServerInfo> Servers { get; set; } = new List<ServerInfo>();
}