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

    public Task<bool> HasProgress() => Task.FromResult(Content is not null);

    public Task<string?> Load() => Task.FromResult(Content);

    /// <summary>
    /// The save that was set aside, so a test can show a refused save is still recoverable.
    /// </summary>
    public string? SetAsideContent { get; private set; }

    /// <summary>
    /// How many times a save was set aside, so a test can show that none was.
    /// </summary>
    public int SetAsideCount { get; private set; }

    public Task Save(string content)
    {
        Content = content;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SetAside()
    {
        SetAsideCount++;

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
