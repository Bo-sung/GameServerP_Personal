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
        static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5001;
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
