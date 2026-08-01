using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// A game in progress: the player, their quests, and the save game the two are persisted to.
/// The screen that owns gameplay begins a session when it is entered.
/// </summary>
/// <remarks>
/// The session holds state and persists it; it does not drive quests. Whatever watches the world
/// starts and completes them, and the session saves when they do.
/// </remarks>
public interface IGameSession
{
    /// <summary>
    /// Gets the player entity.
    /// </summary>
    Player Player { get; }

    /// <summary>
    /// Gets the ship the player has been awarded, and is flying.
    /// </summary>
    /// <remarks>
    /// Part of what a game <em>is</em> rather than something a screen looks up: it is saved with
    /// the session and restored with it, so whatever draws the world can ask the session what the
    /// player is flying rather than being told at wiring time and going stale the moment the ship
    /// changes. Until a game has been started or resumed it reports the ship a new game would
    /// award, so nothing has to handle a player with no ship at all.
    /// </remarks>
    Ship Ship { get; }

    /// <summary>
    /// Gets the player's quests.
    /// </summary>
    QuestLog Quests { get; }

    /// <summary>
    /// Gets a value indicating whether a game has been started or resumed. Nothing should drive the
    /// session until it is <c>true</c>, because frames can arrive while a save is still loading.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the most recent automatic save, so a caller that needs the write to have landed —
    /// tests, or a shutdown path — can await it.
    /// </summary>
    Task PendingSave { get; }

    /// <summary>
    /// Discards any game in progress and begins a fresh one: the player goes to the world's start
    /// position, is awarded the shipyard's starting ship, the campaign's quests are registered, and
    /// the new game is saved.
    /// </summary>
    /// <returns>A task that completes once the new game has been saved.</returns>
    Task StartNewGame();

    /// <summary>
    /// Resumes the saved game, falling back to <see cref="StartNewGame"/> when there is no save or
    /// the save cannot be read.
    /// </summary>
    /// <returns>A task that completes once the session is ready.</returns>
    Task Continue();

    /// <summary>
    /// Writes the current player position, ship and quest states to the save game.
    /// </summary>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save();
}
