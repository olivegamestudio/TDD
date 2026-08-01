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
    /// What is appended to <see cref="FilePath"/> to name the file a set-aside save is kept in.
    /// </summary>
    /// <remarks>
    /// Beside the save rather than in a folder of its own, so that whoever is asked for "your save
    /// file" finds this next to it without having been told where else to look. The suffix says
    /// what happened to it rather than what is wrong with it — the game could not read it, which
    /// is not the same as it being damaged, and that distinction is the whole reason it is kept.
    /// </remarks>
    public const string SetAsideSuffix = ".unreadable";

    /// <summary>
    /// Gets the full path of the file progress is saved to.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the full path of the file a set-aside save is kept in.
    /// </summary>
    public string SetAsideFilePath => FilePath + SetAsideSuffix;

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
    public Task SetAside()
    {
        if (File.Exists(FilePath))
        {
            // Moved rather than copied, so that HasProgress goes false and the caller's new game
            // starts from nothing rather than reading the same unusable file again next launch.
            //
            // Overwriting any earlier set-aside save keeps this to one generation. That is a real
            // cost and worth naming: a second refusal displaces the first file kept, which may
            // have been the more valuable of the two. It is still the better way round, because
            // keeping the first instead means one stale file — from a fault fixed several builds
            // ago — permanently refusing to make room for the failure happening now.
            File.Move(FilePath, SetAsideFilePath, overwrite: true);
        }

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
