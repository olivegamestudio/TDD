using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. It owns no state of its own: entering it begins or resumes
/// the game session, and every frame it hands the elapsed time to that session so quests progress.
/// </summary>
/// <param name="session">The game in progress.</param>
public sealed class GameScreen(IGameSession session) : IGameScreen, IActivatable
{
    /// <summary>
    /// Gets the task that begins the game, so a caller can await the save being read. Loading
    /// happens off the frame loop; the session ignores updates until it has finished.
    /// </summary>
    public Task Started { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Begins the game: the saved game is resumed, or a new one is started when there is no save.
    /// </summary>
    /// <returns>Always <see cref="EnterResult.Stay"/>; this screen is where gameplay happens.</returns>
    public EnterResult Enter()
    {
        Started = session.Continue();
        return EnterResult.Stay;
    }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime) => session.Update(frameTime);

    /// <summary>
    /// Leaves the game screen. Progress is saved as it is made, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }
}
