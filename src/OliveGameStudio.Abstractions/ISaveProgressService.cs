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
    /// Produces content that was saved in full. A read that overlaps a write produces either the
    /// save as it was before that write or the save as it is after it, never part of one — see
    /// <see cref="Save"/> for why an implementation owes the caller that.
    /// </remarks>
    /// <returns>A task producing the saved content, or <c>null</c> when there is no save.</returns>
    Task<string?> Load();

    /// <summary>
    /// Writes the saved progress, replacing anything previously saved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two writes may overlap, and an implementation has to survive it.</b> When they do, the
    /// saved progress ends up holding the whole of exactly one of them. It never holds part of one,
    /// or part of each — and neither call fails on account of the other, because two callers
    /// arriving at once is not the storage getting in the way and must not be reported as though it
    /// were. The same holds for a read overlapping a write.
    /// </para>
    /// <para>
    /// This is the implementation's to keep rather than the caller's, because only the
    /// implementation knows how the bytes get written. It is worth stating because the obvious
    /// way to write a file gives none of it: overlapping writes silently interleave, silently
    /// succeed, or throw, depending on the platform. A save that has been torn in half is not a
    /// damaged file a player can be warned about — a game that refuses what it cannot read will
    /// replace it with a new one, so the campaign is simply gone.
    /// </para>
    /// <para>
    /// <b>Which of two overlapping writes survives is not defined</b>, and callers must not assume
    /// the last one to start is the one that lands. Only the caller knows which of two snapshots is
    /// the newer, so a caller that cares has to wait for the first write before beginning the
    /// second. That is the caller's half of this, and it is the half an implementation cannot do
    /// for them.
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
    /// </remarks>
    /// <returns>A task that completes once the save has been moved.</returns>
    Task SetAside();
}
