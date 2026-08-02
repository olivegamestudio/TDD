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
    /// <remarks>
    /// <para>
    /// One open, rather than asking whether the file is there and then reading it. Those were two
    /// questions with a gap between them, and <see cref="SetAside"/> moves the save out from
    /// exactly that gap — so a read that had passed the existence check found nothing to open and
    /// threw instead of answering. Opening once asks the question and gets the answer in the same
    /// breath.
    /// </para>
    /// <para>
    /// A save that has gone by the time it is read is reported as no save, because that is what it
    /// is: the file really is not there, and the alternative is telling the game its storage is
    /// broken over a read that was only unlucky in its timing. That is not the same as being
    /// unable to read the save — anything else in the way of the read still raises, so a game that
    /// distinguishes the two still can.
    /// </para>
    /// <para>
    /// The share mode admits a concurrent move as well as a concurrent read, so a set-aside is not
    /// blocked by a read already in progress on platforms that enforce sharing.
    /// </para>
    /// </remarks>
    public async Task<string?> Load()
    {
        try
        {
            await using FileStream file = new(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using StreamReader reader = new(file);
            return await reader.ReadToEndAsync();
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // there is no save, which is an answer rather than a failure
            return null;
        }
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
    /// <para>
    /// One generation: a second set-aside replaces the first. An unbounded pile of them is its own
    /// kind of mess in a folder the player may go looking through, and the most recent refusal is
    /// the one a later build would be asked to read.
    /// </para>
    /// <para>
    /// This is the only operation that makes the save path stop existing, which is why
    /// <see cref="Load"/> is written to survive it: a read overlapping a set-aside is answered as
    /// "no save" rather than failing. Nothing orders the two against each other, so a game that
    /// needs a set-aside to be the last word on a file has to arrange that itself.
    /// </para>
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
