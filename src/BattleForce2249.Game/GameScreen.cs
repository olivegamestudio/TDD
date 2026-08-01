using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. Entering it begins or resumes the game session, and every
/// frame it flies the ship, then measures the player against the quest markers and drives the
/// quest API from what it finds — proximity is the presentation's job, not the quest model's.
/// </summary>
/// <param name="session">The game in progress.</param>
/// <param name="ship">The ship's physics, which carries the player around the world.</param>
/// <param name="pilot">Where the pilot's intent comes from this frame.</param>
/// <param name="questProximity">Applies the quests' proximity triggers against the world.</param>
public sealed class GameScreen(
    IGameSession session,
    ShipMovement ship,
    IShipInput pilot,
    QuestProximityWatcher questProximity)
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
        // a fresh flight every time the screen is entered: the save carries where the player is,
        // never how fast they were going, so nothing should be inherited from a previous session
        ship.Reset();

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

        // the ship flies first, so the quests are measured against where the player got to this
        // frame rather than where they were at the end of the last one
        ship.Update(session.Player, pilot.Read(), frameTime);

        questProximity.Update(session.Quests, session.Player.Position);
    }

    /// <summary>
    /// Leaves the game screen. Progress is saved as quests change, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }
}
