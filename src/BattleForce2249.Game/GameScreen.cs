using Microsoft.Extensions.Logging;
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
/// <param name="logger">Where a game that failed to begin is reported.</param>
public sealed class GameScreen(
    IGameSession session,
    ShipMovement ship,
    IShipInput pilot,
    QuestProximityWatcher questProximity,
    ILogger<GameScreen> logger) : IGameScreen, IActivatable
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

        Started = Start();
        return EnterResult.Stay;
    }

    /// <summary>
    /// Begins the game and makes sure a failure is heard.
    /// </summary>
    /// <remarks>
    /// Nothing in the game holds <see cref="Started"/>, so a failure here reaches no one on its own:
    /// the session never becomes ready, every frame turns straight back, and the player sits in
    /// front of a game that has quietly stopped. The session already recovers from a save it cannot
    /// read, so anything landing here is a defect — and it gets logged as one rather than swallowed.
    /// </remarks>
    async Task Start()
    {
        try
        {
            await session.Continue();
        }
        catch (Exception error)
        {
            logger.LogError(error, "The game could not be started, so the game screen will do nothing.");
            throw;
        }
    }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        if (!session.IsReady)
        {
            return;
        }

        // where this frame's travel begins, kept so the quests can be measured against the ground
        // the ship covers rather than the point it happens to finish on. Read here rather than
        // remembered between frames, which is what keeps both this screen and the watcher free of
        // memory — and means a player who was placed rather than flown, entering the screen or
        // resuming a save, does not drag a phantom journey behind them from the last game.
        Position from = session.Player.Position;

        // the ship flies first, so the quests are measured against the journey this frame made
        // rather than the one before it
        ship.Update(session.Player, pilot.Read(), frameTime);

        questProximity.Update(session.Quests, from, session.Player.Position);
    }

    /// <summary>
    /// Leaves the game screen. Progress is saved as quests change, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }
}
