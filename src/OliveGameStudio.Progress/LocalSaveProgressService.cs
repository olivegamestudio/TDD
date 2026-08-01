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

    /// <summary>
    /// The default save location: the studio's folder under the user's application data.
    /// </summary>
    static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OliveGameStudio",
        "save.json");
}
