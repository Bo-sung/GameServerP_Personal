using System;
using System.Net.Sockets;
using System.Threading.Tasks;

/// <summary>
/// 메인 프로그램 - 로그인 서버 → 게임/채팅 서버 순차 연결
/// </summary>
internal class Program
{
    private static string defaultHost = "127.0.0.1";
    private static int defaultPort = 5000;

    public static async Task Main(string[] args)
    {
        defaultHost = args.Length > 0 ? args[0] : "127.0.0.1";
        defaultPort = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 5000;

        Console.WriteLine("=== 클라이언트 시작 ===");
        Console.WriteLine($"기본 서버: {defaultHost}:{defaultPort}");

        try
        {
            // 1단계: 로그인 서버 접속 및 로그인
            var loginResult = await ConnectToLoginServerAsync();

            if (!loginResult.Success)
            {
                Console.WriteLine("[CLIENT] 로그인 실패. 프로그램을 종료합니다.");
                Console.WriteLine($"[CLIENT] 실패 사유: {loginResult.Message}");
                return;
            }

            Console.WriteLine($"\n[CLIENT] 로그인 성공!");
            Console.WriteLine($"[CLIENT] 사용자: {loginResult.Username}");
            Console.WriteLine($"[CLIENT] 발견된 서버 수: {loginResult.Servers.Count}개");

            // 서버 목록 출력
            foreach (var server in loginResult.Servers)
            {
                Console.WriteLine($"[CLIENT] - {server.Type}: {server.Address}");
            }

            // 2단계: 다음 서버 선택 및 연결
            await HandleServerSelection(loginResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLIENT] 전체 오류: {ex.Message}");
        }

        Console.WriteLine("\n프로그램을 종료합니다. 아무 키나 누르세요...");
        Console.ReadKey();
    }

    /// <summary>
    /// 로그인 서버 접속 및 로그인 처리
    /// </summary>
    private static async Task<LoginResult> ConnectToLoginServerAsync()
    {
        Console.WriteLine($"\n=== 로그인 서버 접속 ({defaultHost}:{defaultPort}) ===");

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(defaultHost, defaultPort);
            Console.WriteLine($"[LOGIN] 로그인 서버 연결 성공");

            var handler = new LoginServerHandler();
            var handlerTask = handler.HandleMainLoopAsync(client);

            // 사용자 입력 받기
            Console.Write("사용자명: ");
            var username = Console.ReadLine();
            Console.Write("패스워드: ");
            var password = Console.ReadLine();

            // 로그인 요청 전송
            handler.SendLoginRequest(username, password);

            // 로그인 결과 대기
            var result = await handler.WaitForLoginResult(5000);

            // 핸들러 종료
            handler.Shutdown();

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOGIN] 로그인 서버 접속 실패: {ex.Message}");
            return new LoginResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 서버 선택 및 연결 처리
    /// </summary>
    private static async Task HandleServerSelection(LoginResult loginResult)
    {
        while (true)
        {
            var nextServer = SelectNextServer();

            switch (nextServer)
            {
                case "quit":
                    Console.WriteLine("[CLIENT] 프로그램을 종료합니다.");
                    return;
                default:
                    Console.WriteLine("[CLIENT] 잘못된 선택입니다.");
                    continue;
            }

            Console.WriteLine("\n다른 서버에 연결하시겠습니까?");
        }
    }

    /// <summary>
    /// 다음 서버 선택
    /// </summary>
    private static string SelectNextServer()
    {
        Console.WriteLine("\n=== 서버 선택 ===");
        Console.WriteLine("1. 게임 서버 접속");
        Console.WriteLine("2. 채팅 서버 접속");
        Console.WriteLine("3. 프로그램 종료");
        Console.Write("선택 (1-3): ");

        var input = Console.ReadLine();

        return input switch
        {
            "1" => "game",
            "2" => "chat",
            "3" => "quit",
            _ => "invalid"
        };
    }
}