using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// An <see cref="ISaveProgressService"/> whose reads or writes fail the way a real save file does
/// when it is locked by cloud sync or antivirus, sits on a full disk, or is barred by permissions.
/// The save these failures are protecting is still held, so a test can show it survived.
/// </summary>
/// <param name="loadError">Thrown by <see cref="Load"/>, or <c>null</c> to let reads succeed.</param>
/// <param name="saveError">Thrown by <see cref="Save"/>, or <c>null</c> to let writes succeed.</param>
/// <param name="setAsideError">
/// Thrown by <see cref="SetAside"/>, or <c>null</c> to let it succeed. A refused save that cannot
/// even be moved out of the way is the case where preserving it and replacing it are in direct
/// conflict, so it needs to be reachable from a test.
/// </param>
public sealed class FailingSaveProgressService(
    Exception? loadError = null,
    Exception? saveError = null,
    Exception? setAsideError = null)
    : ISaveProgressService
{
    /// <summary>
    /// Gets or sets the save the service is holding, which a failing read never hands over and a
    /// failing write never replaces.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets the number of writes that landed, so a test can show that none did.
    /// </summary>
    public int SaveCount { get; private set; }

    public Task<bool> HasProgress() => Task.FromResult(Content is not null);

    public Task<string?> Load() =>
        loadError is null ? Task.FromResult(Content) : throw loadError;

    public Task Save(string content)
    {
        if (saveError is not null)
        {
            throw saveError;
        }

        Content = content;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SetAside()
    {
        if (setAsideError is not null)
        {
            throw setAsideError;
        }

        Content = null;
        return Task.CompletedTask;
    }
}
