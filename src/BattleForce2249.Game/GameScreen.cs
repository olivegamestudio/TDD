using Microsoft.Extensions.Logging;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. Entering it begins or resumes the game session, and every
/// frame it measures the player against the quest markers and drives the quest API from what it
/// finds — proximity is the presentation's job, not the quest model's.
/// </summary>
/// <remarks>
/// It measures the ground the player covered rather than the point they finished the frame on, so
/// no frame is long enough to step over a marker. Both ends of that journey come from the player,
/// which is why nothing here remembers anything from frame to frame.
/// </remarks>
/// <param name="session">The game in progress.</param>
/// <param name="questProximity">Applies the quests' proximity triggers against the world.</param>
/// <param name="logger">Where a game that failed to begin is reported.</param>
public sealed class GameScreen(
    IGameSession session,
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

        // the ground covered since the last frame, not the point it ended on: a marker the player
        // flew straight through in one frame is a marker they reached
        questProximity.Update(session.Quests, session.Player.TravelledFrom, session.Player.Position);
    }

    /// <summary>
    /// Leaves the game screen. Progress is saved as quests change, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }
}
