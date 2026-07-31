namespace Pilgrimage;

/// <summary>
/// A game in progress: the player, their quests, and the save game the two are persisted to.
/// The screen that owns gameplay begins a session when it is entered and advances it once a frame.
/// </summary>
public interface IGameSession
{
    /// <summary>
    /// Gets the player entity, whose position drives quest progress.
    /// </summary>
    Player Player { get; }

    /// <summary>
    /// Gets the player's quests.
    /// </summary>
    QuestLog Quests { get; }

    /// <summary>
    /// Gets a value indicating whether a game has been started or resumed. <see cref="Update"/>
    /// does nothing until it is <c>true</c>, because frames can arrive while a save is still loading.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the most recent automatic save, so a caller that needs the write to have landed —
    /// tests, or a shutdown path — can await it.
    /// </summary>
    Task PendingSave { get; }

    /// <summary>
    /// Discards any game in progress and begins a fresh one: the player goes to the campaign start,
    /// quests that auto start there begin, and the new game is saved.
    /// </summary>
    /// <returns>A task that completes once the new game has been saved.</returns>
    Task StartNewGame();

    /// <summary>
    /// Resumes the saved game, falling back to <see cref="StartNewGame"/> when there is no save or
    /// the save cannot be read.
    /// </summary>
    /// <returns>A task that completes once the session is ready to update.</returns>
    Task Continue();

    /// <summary>
    /// Advances the session by one frame, progressing quests against the player's position.
    /// </summary>
    /// <param name="frameTime">The time that has elapsed since the last frame.</param>
    void Update(TimeSpan frameTime);

    /// <summary>
    /// Writes the current player position and quest states to the save game.
    /// </summary>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save();
}
