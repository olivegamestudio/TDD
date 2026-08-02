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
    /// A save that goes while it is being read — set aside by something else, or removed from
    /// under the read — is reported as no save rather than as a failure, because that is the true
    /// answer: there is nothing there. Reporting it as storage trouble would have a game hold its
    /// saving back over a read that was only unlucky in its timing. Being unable to read a save
    /// that <em>is</em> there is a different thing and still raises.
    /// </remarks>
    /// <returns>A task producing the saved content, or <c>null</c> when there is no save.</returns>
    Task<string?> Load();

    /// <summary>
    /// Writes the saved progress, replacing anything previously saved.
    /// </summary>
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
