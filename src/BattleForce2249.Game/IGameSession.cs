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
    /// Gets the error from the last attempt to read, write or set aside the save game, or
    /// <c>null</c> when the save is healthy. It is the only sign the player's progress is not being
    /// kept, so whatever can tell them has something to read.
    /// </summary>
    /// <remarks>
    /// A save that could not be <em>read</em>, or a refused one that could not be <em>set
    /// aside</em>, also stops the session saving, and <see cref="IsSavingProgress"/> says so. A
    /// save that could not be <em>written</em> does not: the next quest to change tries again,
    /// because the failure may have been momentary.
    /// </remarks>
    Exception? SaveError { get; }

    /// <summary>
    /// Gets a value indicating whether quest progress is being written to the save game. It is
    /// <c>false</c> when the game was begun over a save that could not be read, or over a refused
    /// save that could not be moved out of the way, because a save the player may still own must
    /// not be overwritten by the new game standing in for it.
    /// </summary>
    bool IsSavingProgress { get; }

    /// <summary>
    /// Discards any game in progress and begins a fresh one: the player goes to the world's start
    /// position, the campaign's quests are registered, and the new game is saved.
    /// </summary>
    /// <returns>A task that completes once the new game has been saved.</returns>
    Task StartNewGame();

    /// <summary>
    /// Resumes the saved game, falling back to a fresh one when there is no save or the save cannot
    /// be read. It leaves the session ready to play whichever way it goes: a player who cannot be
    /// given their save back is still owed a game, not a screen where nothing ever happens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two failures are not the same and are not treated the same. A save that is
    /// <em>damaged</em> cannot be resumed from, so a new game replaces it — but the refused content
    /// is set aside first, because refusing it is this build's judgement and a later build may read
    /// it perfectly well. A save that could not be <em>read</em> — locked by cloud sync or
    /// antivirus, or unreadable through a permissions problem — may be perfectly intact, so the new
    /// game is played but not saved, and <see cref="SaveError"/> says why.
    /// </para>
    /// <para>
    /// The one case where they meet is a refused save that cannot be moved out of the way: nothing
    /// is written there either, because overwriting it would destroy the very thing being kept.
    /// </para>
    /// <para>
    /// A save that <em>is</em> resumed is resumed at its coordinates only when it restored progress
    /// against a quest this build ships. A readable save can restore none — drift is tolerated in
    /// both directions — and a position without the progress it was taken beside puts the player
    /// outside a campaign nobody has begun, too far from its first trigger to reach it by playing
    /// forward. The file is kept either way; only the position is declined.
    /// </para>
    /// </remarks>
    /// <returns>A task that completes once the session is ready.</returns>
    Task Continue();

    /// <summary>
    /// Writes the current player position and quest states to the save game.
    /// </summary>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save();
}
