using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// An <see cref="ISaveProgressService"/> that keeps the save in memory, so the session tests can
/// assert on what was written and replay it without touching the file system.
/// </summary>
public sealed class InMemorySaveProgressService : ISaveProgressService
{
    public string? Content { get; set; }

    public int SaveCount { get; private set; }

    /// <summary>
    /// What was moved out of the way by <see cref="SetAside"/>, or <c>null</c> if nothing was —
    /// the in-memory stand-in for the file kept beside the save.
    /// </summary>
    public string? SetAsideContent { get; private set; }

    public Task<bool> HasProgress() => Task.FromResult(Content is not null);

    public Task<string?> Load() => Task.FromResult(Content);

    public Task Save(string content)
    {
        Content = content;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SetAside()
    {
        if (Content is not null)
        {
            SetAsideContent = Content;
            Content = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The save as a <see cref="SaveGame"/>, or <c>null</c> if nothing has been written yet.
    /// </summary>
    public SaveGame? Saved => SaveGameSerializer.Deserialize(Content);
}
