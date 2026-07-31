using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. Entering it begins or resumes the game session, and every
/// frame it measures the player against the quest markers and drives the quest API from what it
/// finds — proximity is the presentation's job, not the quest model's.
/// </summary>
/// <param name="session">The game in progress.</param>
/// <param name="questProximity">Applies the quests' proximity triggers against the world.</param>
public sealed class GameScreen(IGameSession session, QuestProximityWatcher questProximity)
    : IGameScreen, IActivatable
{
    /// <summary>
    /// Gets the task that begins the game, so a caller can await the save being read. Loading
    /// happens off the frame loop; nothing drives the session until it has finished.
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
    public void Update(TimeSpan frameTime)
    {
        if (!session.IsReady)
        {
            return;
        }

        questProximity.Update(session.Quests, session.Player.Position);
    }

    /// <summary>
    /// Leaves the game screen. Progress is saved as quests change, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }
}
