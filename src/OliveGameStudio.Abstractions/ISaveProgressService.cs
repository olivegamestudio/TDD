namespace OliveGameStudio;

/// <summary>
/// Stores a game's saved progress. The engine does not know what a save contains — a game
/// serialises its own state and hands it over as text — so the same service works for any game
/// built on Olive Game Studio.
/// </summary>
public interface ISaveProgressService
{
    /// <summary>
    /// Determines whether there is saved progress to continue from. Used by the menu to decide
    /// whether the player is resuming or starting fresh.
    /// </summary>
    /// <returns>A task producing <c>true</c> when a save exists.</returns>
    Task<bool> HasProgress();

    /// <summary>
    /// Reads the saved progress.
    /// </summary>
    /// <remarks>
    /// A read that overlaps a write produces the whole of the save as it was before that write or
    /// the whole of it as it is after, never part of one and never a failure on account of the
    /// write. A read that overlaps a <see cref="SetAside"/> answers one of those or <c>null</c> —
    /// the save really has gone, and that is an answer rather than an error.
    /// </remarks>
    /// <returns>A task producing the saved content, or <c>null</c> when there is no save.</returns>
    Task<string?> Load();

    /// <summary>
    /// Writes the saved progress, replacing anything previously saved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Two writes that overlap leave the whole of one of them.</strong> An implementation
    /// must never leave a file holding bytes from both, and neither call may fail on account of the
    /// other — an overlap is not storage trouble and must not be reported as any, because a caller
    /// cannot tell the difference and a game showing "could not save" for a write that was merely
    /// unlucky in its timing is worse than the overlap. Which of the two survives is the last one
    /// to <em>start</em>.
    /// </para>
    /// <para>
    /// <em>Which</em> is a weaker promise than callers usually want, and deliberately: the service
    /// is handed two pieces of text and has no way to tell which describes the later game. Ordering
    /// two snapshots is the caller's, because only the caller knows which it took second — see
    /// <c>BattleForce2249.GameSession</c>, which queues its writes rather than issuing them
    /// together. What this contract promises is that overlapping callers cannot destroy the save
    /// between them, which is not something a caller can arrange for itself.
    /// </para>
    /// <para>
    /// The rule holds however the overlap arises — two callers of one service, two services, or
    /// two processes — so an implementation cannot keep it by serialising alone. A torn save is not
    /// a damaged file the player can be warned about: the game refuses what it cannot read and
    /// plays a new game over it, so tearing costs the campaign.
    /// </para>
    /// </remarks>
    /// <param name="content">The content to save.</param>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save(string content);

    /// <summary>
    /// Moves the saved progress out of the way, keeping it somewhere it could be recovered from
    /// and leaving no saved progress behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a game that has read its save and decided it cannot resume from it. That decision is a
    /// judgement — a shape this build does not accept may be one a later build reads perfectly
    /// well — and without this the game's only options are to write over the file or to stop
    /// saving. Neither is good enough: the first makes every judgement final, and the second costs
    /// the player the game they are playing now.
    /// </para>
    /// <para>
    /// The engine still learns nothing about what a save contains. Whether the content is worth
    /// keeping is the game's call; this only provides somewhere to put it.
    /// </para>
    /// <para>
    /// Does nothing when there is no saved progress, so a first-time player is not left something
    /// that looks recoverable and is not. How many generations are kept is up to the
    /// implementation.
    /// </para>
    /// <para>
    /// A set-aside counts as a write for <see cref="Save"/>'s rule about overlapping operations: it
    /// may not tear what it keeps, and it may not make a <see cref="Load"/> or a
    /// <see cref="Save"/> beside it fail. It is the one operation that leaves the save's path
    /// naming nothing, so a read overlapping it may answer <c>null</c> where it would otherwise
    /// have answered a save — the save is genuinely gone, and saying so is not a failure. What
    /// callers cannot get from the service is an <em>order</em> between a set-aside and a write;
    /// that is the caller's, for the same reason ordering two writes is.
    /// </para>
    /// </remarks>
    /// <returns>A task that completes once the save has been moved.</returns>
    Task SetAside();
}
