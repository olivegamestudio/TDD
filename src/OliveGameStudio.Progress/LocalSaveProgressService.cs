namespace OliveGameStudio;

/// <summary>
/// Saves progress to a file on the machine the game is running on. The save folder is created on
/// first write, so a fresh install needs no setup.
/// </summary>
/// <remarks>
/// <para>
/// It keeps <see cref="ISaveProgressService.Save"/>'s promise about overlapping writes in two
/// independent ways, because they cover different callers.
/// </para>
/// <para>
/// <b>Within one service, operations on the file take it in turns.</b> Two writes cannot be
/// running together, and a read cannot be running while a write is, so no caller sees a
/// half-written save and no caller is refused because another was busy.
/// </para>
/// <para>
/// <b>A save is written elsewhere and moved into place.</b> Moving a file into place is a single
/// step, so a save is replaced whole or not at all — which is what protects the file from a second
/// process, another service on the same path, or this one being killed halfway through a write.
/// The turn-taking above cannot help with any of those, because it only knows about its own
/// callers.
/// </para>
/// <para>
/// What is deliberately <em>not</em> promised is which of two overlapping writes ends up on disk.
/// Only the caller knows which of two snapshots is the newer one, so a caller that cares must not
/// start the second before the first has finished.
/// </para>
/// </remarks>
public sealed class LocalSaveProgressService : ISaveProgressService
{
    /// <summary>
    /// Held for the whole of any operation that reads or replaces the file, so they take it in
    /// turns. Asynchronous rather than a lock, because these operations await.
    /// </summary>
    readonly SemaphoreSlim _oneAtATime = new(1, 1);


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
    /// <remarks>
    /// Not taking its turn with the rest, because it asks one question of the file system and a
    /// save either exists or does not — there is no half-written state for it to see. Waiting
    /// behind a write to be told what is already true would only make the menu slower.
    /// </remarks>
    public Task<bool> HasProgress() => Task.FromResult(File.Exists(FilePath));

    /// <inheritdoc />
    public async Task<string?> Load()
    {
        await _oneAtATime.WaitAsync();
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return await File.ReadAllTextAsync(FilePath);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The content goes to a file of its own first and is moved over the save once it is all
    /// there. Writing straight into the save would leave it truncated and then partly filled for
    /// as long as the write took — a window in which a reader, or a crash, finds a save that is
    /// neither the old game nor the new one. The game refuses a save it cannot parse, so that
    /// window costs a campaign rather than a moment.
    /// </remarks>
    public async Task Save(string content)
    {
        await _oneAtATime.WaitAsync();
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Named for this write alone, so that two services on one path — or two copies of the
            // game — cannot end up filling the same half-written file and then moving it into
            // place. Beside the save, because a move is only one step when it stays on the volume.
            string writingTo = $"{FilePath}.{Guid.NewGuid():N}.writing";
            try
            {
                await File.WriteAllTextAsync(writingTo, content);
                File.Move(writingTo, FilePath, overwrite: true);
            }
            finally
            {
                // A write that failed must not leave its workings in the player's save folder. A
                // process killed outright still can; that file is not the save and the save it
                // failed to replace is untouched, which is the outcome worth protecting.
                if (File.Exists(writingTo))
                {
                    File.Delete(writingTo);
                }
            }
        }
        finally
        {
            _oneAtATime.Release();
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
        await _oneAtATime.WaitAsync();
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
            _oneAtATime.Release();
        }
    }

    /// <summary>
    /// The default save location: the studio's folder under the user's application data.
    /// </summary>
    static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OliveGameStudio",
        "save.json");
}
