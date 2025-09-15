using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    public class User
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

        public User(TcpClient client)
        {
            if (client == null)
                return;
            this.m_TCP_Client = client;
            this.m_networkStream = client.GetStream();
            this.m_readerStream = new StreamReader(m_networkStream, Encoding.UTF8);
            this.m_writerStream = new StreamWriter(m_networkStream, Encoding.UTF8) { AutoFlush = true };
        }

        ~User()
        {
            Close();
        }

        public virtual void SendString(string? value, bool _isLog = false)
        {
            Send(value, _isLog);
        }

        public virtual void Send(Protocol proto, bool _isLog = false)
        {
            string protoStr = proto.ToString();
            if (_isLog)
                Console.WriteLine(protoStr);
            this.m_writerStream?.WriteLine(protoStr);
        }

        public virtual void Send(string? value, bool _isLog = false)
        {
            if (_isLog)
                Console.WriteLine(value);
            this.m_writerStream?.WriteLine(value);
        }

        public virtual string? ReceiveString()
        {
            return this.m_readerStream?.ReadLine();
        }

        public virtual void Close()
        {
            if (this.m_TCP_Client != null)
            {
                this.m_TCP_Client.Close();
                this.m_writerStream.Close();
                this.m_readerStream.Close();
                this.m_networkStream.Close();
            }
        }
    }
}
