using CommonLib.Commands;
using System.Threading.Tasks;

namespace LobbyServer.Core.Game
{
    public interface ICommandSender
    {
        Task SendCommandToGame(Command command);
    }
}
