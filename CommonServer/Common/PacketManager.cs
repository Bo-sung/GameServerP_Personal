using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonServer.Shared
{
    public static class PacketManager
    {
        private static readonly Dictionary<int, Type> _packetTypes = new Dictionary<int, Type>();

        static PacketManager()
        {
            var packetTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(BasePacket).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in packetTypes)
            {
                var tempInstance = (BasePacket)Activator.CreateInstance(type);
                if (tempInstance != null && !_packetTypes.ContainsKey(tempInstance.ProtocolID))
                {
                    _packetTypes.Add(tempInstance.ProtocolID, type);
                }
            }
        }

        public static BasePacket? CreatePacket(int protocolId)
        {
            if (_packetTypes.TryGetValue(protocolId, out var type))
            {
                return (BasePacket)Activator.CreateInstance(type);
            }
            return null;
        }
    }
}
