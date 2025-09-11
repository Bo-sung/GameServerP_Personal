
using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;


internal class Program
{

    public static void Main(string[] args)
    {
        string host = args.Length > 0 ? args[0] : "127.0.0.1";
        int port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 5000;

        try
        {
            using var client = new TcpClient();
            client.Connect(host, port);
            TicTacToeClient.HandleMainLoop(client);

            Console.WriteLine($"[CLIENT] Connected to {host}:{port}");

        }
        catch (Exception ex)
        {
            Console.WriteLine("[CLIENT] Error: " + ex.Message);
        }
    }

}


class TicTacToeClient
{
    const char o = 'O';
    const char x = 'X';
    const char empty = ' ';
    const char Wall = '□';
    public static void DrawBoard(string wire)
    {
        char board(int i) => wire[i] == '-' ? ' ' : wire[i];

        Console.WriteLine();
        Console.WriteLine(" {0} | {1} | {2} ", board(1), board(2), board(3));
        Console.WriteLine("───┼───┼───");
        Console.WriteLine(" {0} | {1} | {2} ", board(4), board(5), board(6));
        Console.WriteLine("───┼───┼───");
        Console.WriteLine(" {0} | {1} | {2} ", board(7), board(8), board(9));
        Console.WriteLine();
    }

    public static void HandleImmediate(string line, ref string? lastBoard)
    {
        // MOVE 직후 서버가 바로 보내는 첫 라인을 여기서 소화
        if (line.StartsWith("BOARD "))
        {
            lastBoard = line.Substring("BOARD ".Length);
            DrawBoard(lastBoard);
        }
        else if (line.StartsWith("RESULT "))
        {
            var result = line.Substring("RESULT ".Length).Trim();
            if (result == "X") Console.WriteLine("You win! 🎉");
            else if (result == "O") Console.WriteLine("Server wins! 🤖");
            else Console.WriteLine("It's a tie. 🤝");
        }
        else if (line.StartsWith("OPPONENT_MOVE "))
        {
            var tok = line.Split(' ');
            Console.WriteLine($"[INFO] Server moved at {tok[1]}.");
        }
        else if (line.StartsWith("BYE"))
        {
            Console.WriteLine("[CLIENT] Game over. Bye!");
            Environment.Exit(0);
        }
        else if (line.StartsWith("INFO "))
        {
            Console.WriteLine(line.Substring(5));
        }
        else if (line.StartsWith("INVALID"))
        {
            Console.WriteLine($"[SERVER] {line}");
        }
        else
        {
            Console.WriteLine($"[SERVER RAW] {line}");
        }
    }

    public static void HandleMainLoop(TcpClient client)
    {
        using var ns = client.GetStream();
        using var reader = new StreamReader(ns, Encoding.UTF8);
        using var writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

        string? lastBoard = null;
        string myTurn = "";
        string oppositeTurn = "";
        while (true)
        {
            string? line = reader.ReadLine();
            if (line == null) { Console.WriteLine("[CLIENT] Disconnected."); break; }
            if (line.StartsWith("BOARD "))
            {
                lastBoard = line.Substring("BOARD ".Length);
                TicTacToeClient.DrawBoard(lastBoard);
            }
            else if (line.StartsWith("YOUR_MOVE"))
            {
                while (true)
                {
                    Console.Write("Enter a number (1-9): ");
                    var input = Console.ReadLine();
                    if (int.TryParse(input, out int n))
                    {
                    awaitable:
                        writer.WriteLine($"MOVE {n}");
                        // 서버가 INVALID를 보낼 수 있으므로 다음 라인을 미리 본다
                        string? resp = reader.ReadLine();
                        if (resp == null) { Console.WriteLine("[CLIENT] Disconnected."); return; }

                        if (resp.StartsWith("INVALID"))
                        {
                            Console.WriteLine($"[SERVER] {resp}");
                            continue; // 다시 입력
                        }
                        // INVALID가 아니면 일반 진행라인일 수 있으므로 처리 루프에 합류
                        TicTacToeClient.HandleImmediate(resp, ref lastBoard);
                    }
                    else
                    {
                        Console.WriteLine("Please enter a valid integer 1-9.");
                    }
                    break; // 사용자 수 하나 처리 후 루프 탈출
                }
            }
            else if (line.StartsWith("OPPONENT_MOVE "))
            {
                var tok = line.Split(' ');
                Console.WriteLine($"[INFO] Server moved at {tok[1]}.");
            }
            else if (line.StartsWith("RESULT "))
            {
                var result = line.Substring("RESULT ".Length).Trim();
                if (result == myTurn) Console.WriteLine("You win! 🎉");
                else if (result == oppositeTurn) Console.WriteLine("Server wins! 🤖");
                else Console.WriteLine("It's a tie. 🤝");
            }
            else if (line.StartsWith("BYE"))
            {
                Console.WriteLine("[CLIENT] Game over. Bye!");
                break;
            }
            else if (line.StartsWith("INFO You are "))
            {
                string temp = line;
                temp = temp.Replace("INFO You are ", "");
                if (temp.Contains("X"))
                {
                    myTurn = "X";
                    oppositeTurn = "O";
                }
                else if (temp.Contains("O"))
                {
                    myTurn = "O";
                    oppositeTurn = "X";
                }
                else
                    return;
            }
            else if (line.StartsWith("INFO "))
            {
                Console.WriteLine(line.Substring(5));
            }
            else if (line.StartsWith("INVALID"))
            {
                Console.WriteLine($"[SERVER] {line}");
            }
            else
            {
                // 예기치 않은 메시지
                Console.WriteLine($"[SERVER RAW] {line}");
            }
        }
    }
}