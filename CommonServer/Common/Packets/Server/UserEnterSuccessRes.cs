using System.IO;

namespace CommonServer.Shared
{
    public class UserEnterSuccessRes : BasePacket
    {
        public override int ProtocolID => PacketID.USER_ENTER_SUCCESS;
        public override ProtocolType Type => ProtocolType.ToClient;

        public override void Serialize(Stream stream) { }
        public override void Deserialize(Stream stream) { }
    }
}
