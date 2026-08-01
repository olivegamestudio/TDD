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
    /// <returns>A task producing the saved content, or <c>null</c> when there is no save.</returns>
    Task<string?> Load();

    /// <summary>
    /// Writes the saved progress, replacing anything previously saved.
    /// </summary>
    /// <param name="content">The content to save.</param>
    /// <returns>A task that completes once the save has been written.</returns>
    Task Save(string content);

    /// <summary>
    /// Sets the current save aside rather than leaving it to be overwritten: it is kept where it
    /// can still be recovered, and no save is left in its place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is on the storage service rather than in the game because moving a file is storage,
    /// and only the storage knows where the save lives. The judgement it acts on is the game's:
    /// the engine never learns what a save contains, so whether the content is usable is not
    /// something this service could decide. A game calls this at the moment it reads a save it
    /// cannot resume from, before it writes a new game over the top.
    /// </para>
    /// <para>
    /// <b>Why it is worth an interface method at all.</b> A game that refuses a save starts a new
    /// one, and the new one is written immediately — so without this, every judgement a game's
    /// save boundary makes is final. A save refused by a rule that turns out to be a shade too
    /// strict is not merely skipped, it is destroyed, and no later build can revisit the decision.
    /// </para>
    /// <para>
    /// Does nothing when there is no save. One set-aside save is kept: setting another aside
    /// replaces it.
    /// </para>
    /// </remarks>
    /// <returns>A task that completes once the save has been set aside.</returns>
    Task SetAside();
}
