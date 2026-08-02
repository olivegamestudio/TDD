using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The default <see cref="IGameSession"/>. It holds the player and the quest log, and persists both
/// through the engine's save progress service whenever a quest starts or completes.
/// </summary>
/// <remarks>
/// <para>
/// Saves are queued, never issued together. The session marks itself ready to play before its
/// first write has landed — loading is deliberately kept off the frame loop — so on any storage
/// that does not answer within a frame the game is already running while a write is outstanding,
/// and the first frame of a new game starts quest 1 and asks for a second write of the same file.
/// Two snapshots of one file with nothing ordering them means the older can land last, and the
/// player is handed back a game in which the opening quest never began, with no file corrupted
/// and nothing raised to say so.
/// </para>
/// <para>
/// Ordering them is the session's job rather than the storage's, because only the session knows
/// which of two snapshots is the newer: the service is handed two pieces of text and has no way to
/// tell them apart. What a service must do when it <em>is</em> written to twice at once — which
/// this no longer does, but another caller still might — is the service's own contract.
/// </para>
/// </remarks>
public sealed class GameSession : IGameSession
{
    readonly ISaveProgressService _saveProgressService;
    readonly ICampaign _campaign;
    readonly IWorld _world;

    /// <summary>
    /// Guards <see cref="_lastWrite"/>. Quest events arrive on the frame loop and a shutdown flush
    /// need not, so the one place the queue is read and replaced is not left to luck.
    /// </summary>
    readonly Lock _saveOrder = new();

    /// <summary>
    /// The write at the back of the queue: every new save waits for it before starting, and then
    /// becomes it. Ordering the writes is the session's job because only the session knows which
    /// of two snapshots is the newer — the storage sees two payloads and no way to tell.
    /// </summary>
    Task _lastWrite = Task.CompletedTask;

    /// <summary>
    /// Whether a quest changing state should write a save. It is turned off while a game is being
    /// set up, so starting or resuming writes at most one save rather than one per quest.
    /// </summary>
    bool _autoSave;

    /// <summary>
    /// Creates a session for the given campaign and world.
    /// </summary>
    /// <param name="saveProgressService">The service the save game is written to and read from.</param>
    /// <param name="campaign">The quests the session plays.</param>
    /// <param name="world">The world it plays them in.</param>
    public GameSession(ISaveProgressService saveProgressService, ICampaign campaign, IWorld world)
    {
        _saveProgressService = saveProgressService;
        _campaign = campaign;
        _world = world;

        Quests.QuestStarted += OnQuestChanged;
        Quests.QuestCompleted += OnQuestChanged;
    }

    /// <inheritdoc />
    public Player Player { get; } = new();

    /// <inheritdoc />
    public QuestLog Quests { get; } = new();

    /// <inheritdoc />
    public bool IsReady { get; private set; }

    /// <inheritdoc />
    public Task PendingSave { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public Exception? SaveError { get; private set; }

    /// <inheritdoc />
    public bool IsSavingProgress => _autoSave;

    /// <inheritdoc />
    public async Task StartNewGame()
    {
        Reset();

        // Ready before the write has landed, on purpose: waiting here would hold the player on a
        // blank screen for as long as storage takes, and it would close only this one pair anyway
        // — any two quest transitions inside one storage latency race the same way. The queue in
        // Save is what actually orders them.
        _autoSave = true;
        IsReady = true;

        await TrySave();
    }

    /// <inheritdoc />
    public async Task Continue()
    {
        SaveError = null;

        string? content;
        try
        {
            content = await _saveProgressService.Load();
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            // The save was not readable this time, which does not mean it is not there. Give the
            // player a game to play, but hold every write back: overwriting a save that was only
            // locked for a moment loses a game that was never actually lost.
            SaveError = error;

            Reset();
            IsReady = true;
            return;
        }

        SaveGame? save = SaveGameSerializer.Deserialize(content);
        if (save is null)
        {
            await StartNewGameOver(content);
            return;
        }

        Reset();
        Player.MoveTo(new Position(save.PlayerX, save.PlayerY));
        Quests.Restore(save.Quests);

        _autoSave = true;
        IsReady = true;
    }

    /// <summary>
    /// Starts a new game in place of a save this build refused, keeping the refused content first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Refusing a save is a judgement, not a measurement. <see cref="SaveGameSerializer"/> decides
    /// what counts as damaged, and a shape this build will not take may be one a later build reads
    /// perfectly well — so writing the new game straight over the file made every one of those
    /// judgements final and irreversible. A player who pressed Continue lost the game they were
    /// trying to continue.
    /// </para>
    /// <para>
    /// So the file is moved aside first, and only then does the new game save. If it cannot be
    /// moved, the new game is played with saving held back rather than written over the top: the
    /// two goals are in direct conflict at that point, and the one that cannot be undone wins.
    /// That is the same answer a save that could not be *read* already gets, for the same reason.
    /// </para>
    /// </remarks>
    /// <param name="content">
    /// What was read, so that nothing is set aside when there was nothing there. A missing or
    /// blank file holds nothing a later build could recover, and keeping it would only leave the
    /// player a file to wonder about.
    /// </param>
    async Task StartNewGameOver(string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                await _saveProgressService.SetAside();
            }
            catch (Exception error) when (IsStorageFailure(error))
            {
                SaveError = error;

                Reset();
                IsReady = true;
                return;
            }
        }

        await StartNewGame();
    }

    /// <inheritdoc />
    public Task Save()
    {
        // The snapshot is taken here, when the save is asked for, and not when its turn to be
        // written comes round — a queued write must carry the progress that prompted it, or the
        // queue would just be several writes of whatever the game happened to reach.
        string content = SaveGameSerializer.Serialize(Capture());

        lock (_saveOrder)
        {
            Task write = WriteAfter(_lastWrite, content);
            _lastWrite = write;
            return write;
        }
    }

    /// <summary>
    /// Writes <paramref name="content"/> once <paramref name="previous"/> has finished, which is
    /// what keeps two writes of the same save off the same storage at once.
    /// </summary>
    /// <remarks>
    /// The failure of the write in front is deliberately not observed here. It belongs to whoever
    /// asked for that write and has already been reported to them — <see cref="TrySave"/> records
    /// it on <see cref="SaveError"/>, and <see cref="Save"/> raises it — so re-raising it would
    /// hand one caller a problem it never had, and swallowing the whole queue behind one bad
    /// moment would undo the decision to keep saving after a failure.
    /// </remarks>
    /// <param name="previous">The write this one waits for.</param>
    /// <param name="content">The snapshot to write.</param>
    async Task WriteAfter(Task previous, string content)
    {
        try
        {
            await previous;
        }
        catch
        {
            // not ours to report
        }

        await _saveProgressService.Save(content);
    }

    /// <summary>
    /// Saves, recording a storage failure rather than raising it. Every automatic save runs through
    /// here, because the alternative is a write failing on a background task nobody is holding —
    /// which is how a game ends up stopped with nothing said. Callers who want the failure raised
    /// use <see cref="Save"/>.
    /// </summary>
    async Task TrySave()
    {
        try
        {
            await Save();
            SaveError = null;
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            // Left saving on: the file may be free again by the next quest, and a game that gives
            // up on saving after one bad moment loses far more than it protects.
            SaveError = error;
        }
    }

    /// <summary>
    /// Whether an exception is the storage getting in the way rather than a defect. These two are
    /// what a file that is missing, locked, on a full disk or barred by permissions throws; catching
    /// wider than this would bury real bugs behind a "could not save" message.
    /// </summary>
    static bool IsStorageFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Returns the session to a freshly registered campaign with the player at the world's start.
    /// </summary>
    void Reset()
    {
        _autoSave = false;
        IsReady = false;

        Quests.Clear();
        foreach (QuestDefinition definition in _campaign.Quests)
        {
            Quests.Register(definition);
        }

        Player.MoveTo(_world.PlayerStart);
    }

    /// <summary>
    /// Takes a snapshot of the session for persisting.
    /// </summary>
    SaveGame Capture() => new()
    {
        PlayerX = Player.Position.X,
        PlayerY = Player.Position.Y,
        Quests = Quests.Capture(),
    };

    /// <summary>
    /// Saves in the background when a quest starts or completes, which is the only progress worth
    /// persisting; saving every frame the player moves would write constantly.
    /// </summary>
    void OnQuestChanged(object? sender, QuestEventArgs e)
    {
        if (_autoSave)
        {
            PendingSave = TrySave();
        }
    }
}
