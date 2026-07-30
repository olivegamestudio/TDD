using OliveGameStudio;

namespace BattleForce2249;

public interface IMenuScreen : IScreen
{
    event EventHandler? StartGameRequested;
}