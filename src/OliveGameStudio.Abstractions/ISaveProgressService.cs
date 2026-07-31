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
}
