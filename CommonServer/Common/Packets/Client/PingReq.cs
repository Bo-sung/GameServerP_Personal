using System.IO;

namespace CommonServer.Shared
{
    public class PingReq : BasePacket
    {
        public override int ProtocolID => PacketID.PING_REQUEST;
        public override ProtocolType Type => ProtocolType.ToServer;

        public override void Serialize(Stream stream) { }
        public override void Deserialize(Stream stream) { }
    }
}
