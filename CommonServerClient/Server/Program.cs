using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Server
{
    class Player
    {
        protected NetworkStream? m_networkStream;
        protected TcpClient? m_TCP_Client;

        /// <summary>
        /// 수신 스트림
        /// </summary>
        protected StreamReader? m_readerStream;

        /// <summary>
        /// 송출 스트림
        /// </summary>
        protected StreamWriter? m_writerStream;

        public EndPoint? EndPoint => m_TCP_Client?.Client.RemoteEndPoint;

        public Player(TcpClient client)
        {
            if (client == null)
                return;
            this.m_TCP_Client = client;
            this.m_networkStream = client.GetStream();
            this.m_readerStream = new StreamReader(m_networkStream, Encoding.UTF8);
            this.m_writerStream = new StreamWriter(m_networkStream, Encoding.UTF8) { AutoFlush = true };
        }

        ~Player()
        {
            if (this.m_TCP_Client != null)
            {
                this.m_TCP_Client.Close();
                this.m_writerStream.Close();
                this.m_readerStream.Close();
                this.m_networkStream.Close();
            }
        }

        public virtual void SendString(string? value, bool _isLog = false)
        {
            if (_isLog)
                Console.WriteLine(value);
            this.m_writerStream.WriteLine(value);
        }

        public virtual string? ReceiveString()
        {
            return this.m_readerStream.ReadLine();
        }
    }

    internal class Program
    {
        class TicTacToeAI : Player
        {
            protected char[] m_board;
            const string BOARD_TEXT = "BOARD ";

            protected string? LastReceived = "";
            public TicTacToeAI(TcpClient client = null) : base(client)
            {
                m_board = new char[] { TicTacToe.EMPTY,
                    TicTacToe.EMPTY, TicTacToe.EMPTY, TicTacToe.EMPTY,
                    TicTacToe.EMPTY, TicTacToe.EMPTY, TicTacToe.EMPTY,
                    TicTacToe.EMPTY, TicTacToe.EMPTY, TicTacToe.EMPTY };
            }

            int GetComputerMove(char[] board)
            {
                for (int i = 1; i < 10; i++)
                {
                    if (TicTacToe.IsFree(board, i))
                    {
                        board[i] = TicTacToe.O;
                        if (TicTacToe.CheckForWin(board, board[i]))
                        {
                            board[i] = TicTacToe.EMPTY;
                            return i;
                        }
                        board[i] = TicTacToe.EMPTY;
                    }
                }

                for (int i = 1; i < 10; i++)
                {
                    if (TicTacToe.IsFree(board, i))
                    {
                        board[i] = TicTacToe.X;
                        if (TicTacToe.CheckForWin(board, board[i]))
                        {
                            board[i] = TicTacToe.EMPTY;
                            return i;
                        }
                        board[i] = TicTacToe.EMPTY;
                    }
                }

                for (int i = 1; i < 10; i += 2)
                {
                    if (i != 5 && TicTacToe.IsFree(board, i))
                        return i;
                }

                if (TicTacToe.IsFree(board, 5))
                    return 5;

                for (int i = 2; i < 10; i += 2)
                {
                    if (TicTacToe.IsFree(board, i))
                        return i;
                }

                return 1; // 접근 불가능해야함.
            }

            public override string? ReceiveString()
            {
                return $"MOVE {GetComputerMove(m_board)}";
            }

            protected char[] WireToBoard(string wire)
            {
                char[] b = new char[10];
                for (int i = 0; i < 10; ++i)
                {
                    b[i] = wire[i] == '-' ? TicTacToe.EMPTY : wire[i];
                }
                return b;
            }

            public override void SendString(string? value, bool _isLog = false)
            {
                LastReceived = value;

                // 보드 정보면 파싱해서 저장
                if (value != null && value.Contains(BOARD_TEXT))
                {
                    m_board = WireToBoard(value.Replace(BOARD_TEXT, ""));
                }
            }
        }

        class TicTacToe
        {
            public const char O = 'O';
            public const char X = 'X';
            public const char EMPTY = ' ';

            public static bool CheckForWin(char[] board, char player)
            {
                return (board[1] == player && board[2] == player && board[3] == player) ||
                        (board[4] == player && board[5] == player && board[6] == player) ||
                        (board[7] == player && board[8] == player && board[9] == player) ||
                        (board[1] == player && board[4] == player && board[7] == player) ||
                        (board[2] == player && board[5] == player && board[8] == player) ||
                        (board[3] == player && board[6] == player && board[9] == player) ||
                        (board[3] == player && board[5] == player && board[7] == player) ||
                        (board[1] == player && board[5] == player && board[9] == player);
            }

            public static bool IsFree(char[] board, int loc)
            {
                return board[loc] == EMPTY;
            }

            public static bool CheckForTie(char[] board)
            {
                for (int i = 1; i < 10; i++)
                {
                    if (IsFree(board, i))
                        return false;
                }

                return true;
            }

            public static string BoardToWire(char[] b)
            {
                var sb = new StringBuilder(9);
                for (int i = 0; i < 10; ++i)
                {
                    sb.Append(b[i] == EMPTY ? '-' : b[i]);
                }
                return sb.ToString();
            }

            static void SendBoard(Player client, char[] b)
            {
                client.SendString("BOARD " + BoardToWire(b));
            }

            static int GetComputerMove(char[] board)
            {
                for (int i = 1; i < 10; i++)
                {
                    if (IsFree(board, i))
                    {
                        board[i] = O;
                        if (CheckForWin(board, board[i]))
                        {
                            board[i] = EMPTY;
                            return i;
                        }
                        board[i] = EMPTY;
                    }
                }

                for (int i = 1; i < 10; i++)
                {
                    if (IsFree(board, i))
                    {
                        board[i] = X;
                        if (CheckForWin(board, board[i]))
                        {
                            board[i] = EMPTY;
                            return i;
                        }
                        board[i] = EMPTY;
                    }
                }

                for (int i = 1; i < 10; i += 2)
                {
                    if (i != 5 && IsFree(board, i))
                        return i;
                }

                if (IsFree(board, 5))
                    return 5;

                for (int i = 2; i < 10; i += 2)
                {
                    if (IsFree(board, i))
                        return i;
                }

                return 1; // 접근 불가능해야함.
            }

            public static void HandleClient_PVE(TcpClient client)
            {
                var myClient = new Player(client);

                Console.WriteLine($"[SERVER] Client connected from {client.Client.RemoteEndPoint}");

                char[] board = { EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY };
                char user = X;
                char ai = O;

                myClient.SendString("INFO Welcom to TicTacToe Server");
                myClient.SendString($"INFO You are '{user}'. Enter moves 1-9.");
                SendBoard(myClient, board);

                bool gameOver = false;

                while (!gameOver)
                {
                    // 1) 사용자 차례
                    myClient.SendString("YOUR_MOVE");

                    while (true)
                    {
                        string? line = myClient.ReceiveString();
                        if (line == null) { Console.WriteLine("[SERVER] Client disconnected."); return; }

                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2 && parts[0].Equals("MOVE", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(parts[1], out int loc))
                        {
                            if (loc < 1 || loc > 9) { myClient.SendString("INVALID OutOfRange"); continue; }
                            if (!IsFree(board, loc)) { myClient.SendString("INVALID Occupied"); continue; }

                            board[loc] = user;
                            SendBoard(myClient, board);

                            if (CheckForWin(board, user))
                            {
                                myClient.SendString($"RESULT {X}");
                                myClient.SendString("BYE");
                                gameOver = true;
                            }
                            else if (CheckForTie(board))
                            {
                                myClient.SendString("RESULT TIE");
                                myClient.SendString("BYE");
                                gameOver = true;
                            }
                            break;
                        }
                        else
                        {
                            myClient.SendString("INVALID Format"); // 예: MOVE 5
                        }
                    }

                    if (gameOver) break;

                    // 2) 서버(AI) 차례
                    int aiMove = GetComputerMove(board);
                    board[aiMove] = ai;
                    myClient.SendString($"OPPONENT_MOVE {aiMove}");
                    SendBoard(myClient, board);

                    if (CheckForWin(board, ai))
                    {
                        myClient.SendString("RESULT O");
                        myClient.SendString("BYE");
                        gameOver = true;
                    }
                    else if (CheckForTie(board))
                    {
                        myClient.SendString("RESULT TIE");
                        myClient.SendString("BYE");
                        gameOver = true;
                    }
                }

                Console.WriteLine("[SERVER] Game finished. Closing connection.");
            }
            public static void HandleClient_PVP(Player[] clients)
            {
                const int MAXPLAYER = 2;

                // 클라이언트 세팅
                for (int i = 0; i < clients.Length; ++i)
                {
                    if (i > MAXPLAYER)
                    {
                        Console.WriteLine($"Over Max Player. kicked {clients[i].EndPoint}");
                        continue;
                    }

                    Console.WriteLine($"[SERVER] Client connected from {clients[i].EndPoint}");
                    clients[i].SendString("INFO Welcom to TicTacToe Server");

                }

                char[] board = { EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY, EMPTY };
                char[] turn = { X, O };

                for (int i = 0; i < MAXPLAYER; ++i)
                {
                    clients[i].SendString($"INFO You are '{turn[i]}'.");
                    SendBoard(clients[i], board);
                }

                bool gameOver = false;
                while (!gameOver)
                {
                    for (int i = 0; i < MAXPLAYER; ++i)
                    {
                        while (true)
                        {
                            clients[i].SendString("YOUR_MOVE. Enter moves 1-9.");
                            string? line = clients[i].ReceiveString();
                            if (line == null)
                            {
                                Console.WriteLine("[SERVER] Client disconnected.");
                                break;
                            }

                            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2 && parts[0].Equals("MOVE", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1], out int location))
                            {
                                if (location < 1 || location > 9)
                                {
                                    clients[i].SendString("INVALID OutOfRange");
                                    continue;
                                }
                                if (!IsFree(board, location))
                                {
                                    clients[i].SendString("INVALID Occupied");
                                    continue;
                                }

                                board[location] = turn[i];
                                // 모든 플레이어한테 보드 전송
                                for (int j = 0; j < MAXPLAYER; ++j)
                                {
                                    SendBoard(clients[j], board);
                                    clients[j].SendString($"OPPONENT_MOVE {location}");

                                    if (CheckForWin(board, turn[i]))
                                    {
                                        clients[j].SendString($"RESULT {turn[i]}");
                                        clients[j].SendString("BYE");
                                        gameOver = true;
                                    }
                                    else if (CheckForTie(board))
                                    {
                                        clients[j].SendString("RESULT TIE");
                                        clients[j].SendString("BYE");
                                        gameOver = true;
                                    }
                                }

                                break;
                            }
                            else
                            {
                                clients[i].SendString("INVALID Format"); // 예: MOVE 5
                            }
                        }
                    }
                }
                Console.WriteLine("[SERVER] Game finished. Closing connection.");
            }
        }

        static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5000;
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"[SERVER] Listening on 0.0.0.0:{port} ...");

            const int MAXPLAYERS = 2;
            TcpClient[] tcpClients = new TcpClient[MAXPLAYERS];
            Player[] players = new Player[MAXPLAYERS];
            int clientIndex = 0;
            while (true)
            {
                tcpClients[clientIndex] = listener.AcceptTcpClient();
                if(clientIndex >= MAXPLAYERS - 1)
                {
                    for (int i = 0; i < MAXPLAYERS; i++)
                    {
                        players[i] = new Player(tcpClients[i]);
                    }

                    TicTacToe.HandleClient_PVP(players);
                    for (int i = 0; i < MAXPLAYERS; i++)
                    {
                        tcpClients[i].Close();
                    }
                    clientIndex = 0;
                    continue;
                }
                clientIndex++;
            }
        }
    }
}
