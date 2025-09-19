using System.IO;

namespace CommonServer.Shared
{
    public class UserLogoutReq : BasePacket
    {
        public override int ProtocolID => PacketID.USER_LOGOUT_REQUEST;
        public override ProtocolType Type => ProtocolType.ToServer;

        public override void Serialize(Stream stream) { }
        public override void Deserialize(Stream stream) { }
    }
}
