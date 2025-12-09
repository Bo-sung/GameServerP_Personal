using System;

namespace CommonLib.Commands
{
    // 기존 BaseServer의 GameCommandType을 CommonLib.Commands로 옮겨옴
    public enum GameCommandType
    {
        ProduceFleet,
        MoveFleet,
        // ... 기타 명령 타입 ...
    }

    [Serializable]
    public abstract class Command
    {
        public int PlayerId { get; set; }
        public abstract GameCommandType Type { get; }
    }
}
