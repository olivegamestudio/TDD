using OliveGameStudio;

namespace BattleForce2249;

public interface ICompanyScreen : IScreen
{
    event EventHandler? Completed;
}