using CommonLib;

namespace CommonLib.Commands
{
    public class ProduceFleetCommand : Command
    {
        private long m_tick = 0;
        private int m_targetId;

        public long TickNumber => m_tick;
        public int TargetId => m_targetId;

        public override GameCommandType Type => GameCommandType.ProduceFleet;

        public ProduceFleetCommand(Protocol protocol)
        {
            if(protocol == null)
                return;

            this.m_tick = protocol.GetParam<long>("tick");
            this.m_targetId = protocol.GetParam<int>("target");
        }
    }
}