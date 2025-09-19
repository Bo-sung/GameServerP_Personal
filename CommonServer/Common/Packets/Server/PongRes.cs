using System.IO;

namespace CommonServer.Shared
{
    public class PongRes : BasePacket
    {
        public override int ProtocolID => PacketID.PONG_RESPONSE;
        public override ProtocolType Type => ProtocolType.ToClient;

        public override void Serialize(Stream stream) { }
        public override void Deserialize(Stream stream) { }
    }
}
