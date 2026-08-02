namespace OliveGameStudio;

/// <summary>
/// Saves progress to a file on the machine the game is running on. The save folder is created on
/// first write, so a fresh install needs no setup.
/// </summary>
public sealed class LocalSaveProgressService : ISaveProgressService
{
    /// <summary>
    /// Creates a service saving to the default location for the current user.
    /// </summary>
    public LocalSaveProgressService()
        : this(DefaultFilePath())
    {
    }

    /// <summary>
    /// Creates a service saving to a specific file, which lets a host choose the location and lets
    /// tests keep out of the real save folder.
    /// </summary>
    /// <param name="filePath">The full path of the save file.</param>
    public LocalSaveProgressService(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the full path of the file progress is saved to.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the full path a save is moved to by <see cref="SetAside"/>: the save file's name with
    /// <c>.corrupt</c> before its extension, in the same folder — <c>save.json</c> becomes
    /// <c>save.corrupt.json</c>.
    /// </summary>
    /// <remarks>
    /// Beside the save rather than anywhere else, so a player who finds one finds the other, and
    /// keeping the extension so it still opens in whatever reads the save. Public because it is
    /// the answer to "where did my game go" — a support question this type would otherwise be the
    /// only thing able to answer.
    /// </remarks>
    public string SetAsideFilePath => Path.Combine(
        Path.GetDirectoryName(FilePath) ?? string.Empty,
        Path.GetFileNameWithoutExtension(FilePath) + ".corrupt" + Path.GetExtension(FilePath));

    /// <inheritdoc />
    public Task<bool> HasProgress() => Task.FromResult(File.Exists(FilePath));

    /// <inheritdoc />
    public async Task<string?> Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(FilePath);
    }

    /// <inheritdoc />
    public async Task Save(string content)
    {
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(FilePath, content);
    }

    /// <inheritdoc />
    /// <remarks>
    /// One generation: a second set-aside replaces the first. An unbounded pile of them is its own
    /// kind of mess in a folder the player may go looking through, and the most recent refusal is
    /// the one a later build would be asked to read.
    /// </remarks>
    public Task SetAside()
    {
        if (!File.Exists(FilePath))
        {
            return Task.CompletedTask;
        }

        // Move rather than copy-then-delete: the point is that no save is left behind, and a move
        // cannot leave both files sitting there if it fails halfway.
        File.Move(FilePath, SetAsideFilePath, overwrite: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The default save location: the studio's folder under the user's application data.
    /// </summary>
    static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OliveGameStudio",
        "save.json");
}
