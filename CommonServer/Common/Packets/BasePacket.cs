using System.IO;

namespace CommonServer.Shared
{
    public abstract class BasePacket
    {
        public abstract int ProtocolID { get; }
        public abstract ProtocolType Type { get; }

        // 객체의 데이터를 스트림에 쓴다
        public abstract void Serialize(Stream stream);

        // 스트림에서 데이터를 읽어 객체를 채운다
        public abstract void Deserialize(Stream stream);
    }
}
