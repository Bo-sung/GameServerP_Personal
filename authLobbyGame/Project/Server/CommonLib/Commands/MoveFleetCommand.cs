using CommonLib;

namespace CommonLib.Commands
{
    // "함대 이동" 명령
    [Serializable]
    public class MoveFleetCommand : Command
    {
        private long m_tick = 0;
        private int m_targetFleet;
        private int m_targetPlanetId;

        public long TickNumber => m_tick;
        public int TargetFleet => m_targetFleet;
        public int TargetPlanetId => m_targetPlanetId;

        public override GameCommandType Type => GameCommandType.MoveFleet;

        public MoveFleetCommand(Protocol protocol)
        {
            if (protocol == null)
                return;

            this.m_tick = protocol.GetParam<long>("tick");
            this.m_targetFleet = protocol.GetParam<int>("target_fleet");
            this.m_targetPlanetId = protocol.GetParam<int>("target_planet");
        }
    }
}