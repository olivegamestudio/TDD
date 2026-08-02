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
    /// <remarks>
    /// Awaiting it waits out everything the session had already been asked to do to the file as
    /// well — earlier saves, and the read or the set-aside that begins a game — because they are
    /// performed one at a time and in the order they were asked for. There is no state of affairs
    /// in which this has completed and something asked for before it has not. A save that was
    /// asked for is always eventually written: nothing abandons one, not even the end of the game
    /// that asked for it.
    /// </remarks>
    Task PendingSave { get; }

    /// <summary>
    /// Gets the error from the last attempt to read or write the save game, or <c>null</c> when the
    /// save is healthy. It is the only sign the player's progress is not being kept, so whatever
    /// can tell them has something to read.
    /// </summary>
    /// <remarks>
    /// A save that could not be <em>read</em> also stops the session saving, and
    /// <see cref="IsSavingProgress"/> says so. A save that could not be <em>written</em> does not:
    /// the next quest to change tries again, because the failure may have been momentary.
    /// </remarks>
    Exception? SaveError { get; }

    /// <summary>
    /// Gets a value indicating whether quest progress is being written to the save game. It is
    /// <c>false</c> when the game was begun over a save that could not be read, because a save the
    /// player may still own must not be overwritten by the new game standing in for it.
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
    /// The two failures are not the same and are not treated the same. A save that is <em>damaged</em>
    /// is gone, so a new game replaces it. A save that could not be <em>read</em> — locked by cloud
    /// sync or antivirus, or unreadable through a permissions problem — may be perfectly intact, so
    /// the new game is played but not saved, and <see cref="SaveError"/> says why.
    /// </remarks>
    /// <remarks>
    /// The game in progress ends before the file is read, and the read waits for anything that
    /// game left outstanding. One session serves every entry into gameplay, so a write it was
    /// asked for by the game before is still to come; reading past it would resume from a snapshot
    /// the file is about to stop holding, and the next quest to change would then write that older
    /// game back out over the player's real progress, with nothing raised to say so.
    /// </remarks>
    /// <returns>A task that completes once the session is ready.</returns>
    Task Continue();

    /// <summary>
    /// Writes the current player position and quest states to the save game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The snapshot is taken when this is called; the write itself waits for any save already
    /// outstanding. So two saves are never in flight against the same storage at once, and the
    /// last one asked for is the last one to land — which is what stops an older snapshot
    /// overwriting a newer one and quietly handing the player back progress they had passed.
    /// </para>
    /// <para>
    /// Only the failure of <em>this</em> write is raised. A save queued in front of this one that
    /// failed was already reported to whoever asked for it, and does not stop this one running:
    /// the storage may be free again by now, and a session that gave up saving after one bad
    /// moment would lose far more than it protects.
    /// </para>
    /// </remarks>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save();
}
