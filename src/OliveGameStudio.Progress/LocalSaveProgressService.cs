namespace OliveGameStudio;

/// <summary>
/// Saves progress to a file on the machine the game is running on. The save folder is created on
/// first write, so a fresh install needs no setup.
/// </summary>
/// <remarks>
/// <para>
/// It keeps <see cref="ISaveProgressService.Save"/>'s promise about overlapping operations in two
/// ways, because one of them alone is not enough.
/// </para>
/// <para>
/// <strong>A gate</strong> — every operation takes <see cref="_gate"/>, so nothing this instance is
/// asked to do overlaps anything else it is doing, and the last write to <em>start</em> is the one
/// left on disk. That is a defined last writer, which the contract needs and which two racing calls
/// to <see cref="File.WriteAllTextAsync(string, string?)"/> do not have.
/// </para>
/// <para>
/// <strong>An atomic replace</strong> — a write goes to a file of its own beside the save and is
/// then moved over it, so the save's own path never names a half-written file. The gate cannot help
/// here: it is per instance, and a second process, a second service or another game built on the
/// engine shares none of it. Moving a finished file into place is what makes an overlap the engine
/// cannot see harmless rather than merely unlikely — and it is also what protects a reader, which
/// no amount of serialising writers would.
/// </para>
/// </remarks>
public sealed class LocalSaveProgressService : ISaveProgressService, IDisposable
{
    /// <summary>
    /// Held for the whole of every operation. One at a time, in the order they arrive.
    /// </summary>
    readonly SemaphoreSlim _gate = new(1, 1);

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
    /// The file system is asked once rather than asked whether the save exists and then told to
    /// open it. Those were two questions with a gap between them, and <see cref="SetAside"/> moves
    /// the save out from under exactly that gap — a read that had passed the existence check found
    /// nothing to open and threw <see cref="FileNotFoundException"/> instead of answering. A save
    /// that is not there when the read reaches it is genuinely no save, and saying so is the true
    /// answer; throwing would reach the player as storage trouble for a read that was only unlucky
    /// in its timing.
    /// </remarks>
    public async Task<string?> Load()
    {
        await _gate.WaitAsync();
        try
        {
            return await File.ReadAllTextAsync(FilePath);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Written beside the save and moved over it, rather than written into it. Writing in place
    /// truncates first, so the save's own path names a half-written file for as long as the write
    /// takes — long enough for a reader to get part of one, and for a second writer to interleave
    /// with it. A move is one step: the path names the old save or the new one and nothing in
    /// between. The temporary file carries a unique name so that two writers cannot collide in it
    /// either, which is the whole point of writing somewhere else.
    /// </remarks>
    public async Task Save(string content)
    {
        await _gate.WaitAsync();
        try
        {
            string directory = EnsureDirectory();
            string pending = Path.Combine(
                directory,
                $"{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.writing");

            try
            {
                await File.WriteAllTextAsync(pending, content);
                File.Move(pending, FilePath, overwrite: true);
            }
            catch
            {
                // A write that failed must not leave a stray file in the player's save folder. It
                // is a best effort: if this cannot be deleted either, the original failure is the
                // one worth reporting.
                TryDelete(pending);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// One generation: a second set-aside replaces the first. An unbounded pile of them is its own
    /// kind of mess in a folder the player may go looking through, and the most recent refusal is
    /// the one a later build would be asked to read.
    /// </remarks>
    public async Task SetAside()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            // Move rather than copy-then-delete: the point is that no save is left behind, and a
            // move cannot leave both files sitting there if it fails halfway.
            File.Move(FilePath, SetAsideFilePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Creates the save folder if it is not there yet and returns it, so a fresh install needs no
    /// setup. A save path with no folder part is written relative to the working directory, which
    /// already exists.
    /// </summary>
    string EnsureDirectory()
    {
        string? directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return ".";
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Deletes a file if it can, and says nothing if it cannot. Only for tidying up after a failure
    /// that is already on its way to the caller.
    /// </summary>
    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Releases the gate. The service is registered as a singleton and lives as long as the game,
    /// so this runs when the host is torn down.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// The default save location: the studio's folder under the user's application data.
    /// </summary>
    static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OliveGameStudio",
        "save.json");
}
