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
/// <para>
/// The queue holds every operation the session performs on the file, not only the writes. One
/// session is registered for the whole game, so it outlives any one game played through it, and a
/// write asked for by a game that is over is still a write: reading the file past it would resume
/// from a snapshot the file is about to stop holding, and moving the file aside past it would let
/// that write recreate a save behind the move. Neither corrupts anything and neither raises
/// anything, which is what makes them worth ordering rather than watching for.
/// </para>
/// </remarks>
public sealed class GameSession : IGameSession
{
    readonly ISaveProgressService _saveProgressService;
    readonly ICampaign _campaign;
    readonly IWorld _world;

    /// <summary>
    /// Guards <see cref="_lastFileOperation"/>. Quest events arrive on the frame loop and a
    /// shutdown flush need not, so the one place the queue is read and replaced is not left to
    /// luck.
    /// </summary>
    readonly Lock _fileOrder = new();

    /// <summary>
    /// The operation at the back of the queue: every new one waits for it before starting, and
    /// then becomes it. Ordering them is the session's job because only the session knows which of
    /// two snapshots is the newer, and which game each of them belonged to — the storage sees a
    /// series of payloads and no way to tell any of that.
    /// </summary>
    Task _lastFileOperation = Task.CompletedTask;

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

        // The game in progress ends here, before the file is touched, rather than once the read
        // has answered. Reset turns autosave off, so a frame arriving while the read is in flight
        // cannot put one last write of the game being left behind the read of the game arriving —
        // which is the leftover write the queue would then be ordering against.
        Reset();

        string? content;
        try
        {
            content = await Queue(_saveProgressService.Load);
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            // The save was not readable this time, which does not mean it is not there. Give the
            // player a game to play, but hold every write back: overwriting a save that was only
            // locked for a moment loses a game that was never actually lost.
            SaveError = error;

            IsReady = true;
            return;
        }

        SaveGame? save = SaveGameSerializer.Deserialize(content);
        if (save is null)
        {
            await StartNewGameOver(content);
            return;
        }

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
                // Queued like a write, because it is the same file. A write already asked for and
                // not yet run would otherwise land after the move and recreate a save behind it,
                // leaving the file set aside no longer the only copy. Queueing costs nothing here:
                // the failure is still this operation's own and still arrives to be caught, since
                // only the operation in front of it has its failure swallowed.
                await Queue(_saveProgressService.SetAside);
            }
            catch (Exception error) when (IsStorageFailure(error))
            {
                SaveError = error;

                // Already reset by Continue, which is this method's only caller and ends the game
                // in progress before it reads the file.
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

        return Queue(() => _saveProgressService.Save(content));
    }

    /// <summary>
    /// Puts <paramref name="operation"/> at the back of the queue, so it starts once everything
    /// already asked for has finished and everything asked for after it waits in turn.
    /// </summary>
    /// <remarks>
    /// Every read, write and move the session makes goes through here, because they all act on one
    /// file and the order they were asked for in is the only order any of them can be right in.
    /// </remarks>
    /// <param name="operation">The work to run when its turn comes.</param>
    /// <returns>A task that completes when this operation has, not when its turn arrives.</returns>
    Task Queue(Func<Task> operation) =>
        Queue<object?>(async () =>
        {
            await operation();
            return null;
        });

    /// <inheritdoc cref="Queue(Func{Task})" />
    /// <typeparam name="T">What the operation produces.</typeparam>
    Task<T> Queue<T>(Func<Task<T>> operation)
    {
        // What the next operation will wait for. It is put at the back of the queue before this
        // one is allowed to start, so that a save asked for *by* an operation already running —
        // a shutdown flush arriving while the save is being read — queues behind it rather than
        // beside it. Claiming the slot afterwards would hand that caller the queue as it stood
        // before this operation joined it.
        TaskCompletionSource finished = new();

        lock (_fileOrder)
        {
            Task previous = _lastFileOperation;
            _lastFileOperation = finished.Task;

            return RunAfter(previous, operation, finished);
        }
    }

    /// <summary>
    /// Runs <paramref name="operation"/> once <paramref name="previous"/> has finished, which is
    /// what keeps two operations off the same file at once, and then releases whatever is queued
    /// behind it.
    /// </summary>
    /// <remarks>
    /// What the next operation waits on always completes successfully, however this one ends. A
    /// failure belongs to whoever asked for it and has already been reported to them —
    /// <see cref="TrySave"/> records it on <see cref="SaveError"/>, and <see cref="Save"/> raises
    /// it — so passing it down the queue would hand one caller a problem it never had, and
    /// abandoning the queue behind one bad moment would undo the decision to keep saving after a
    /// failure. A write that could not be made is also no reason not to set a refused save aside.
    /// </remarks>
    /// <typeparam name="T">What the operation produces.</typeparam>
    /// <param name="previous">The operation this one waits for.</param>
    /// <param name="operation">The work to run once it has.</param>
    /// <param name="finished">Released when this operation ends, however it ends.</param>
    static async Task<T> RunAfter<T>(
        Task previous, Func<Task<T>> operation, TaskCompletionSource finished)
    {
        try
        {
            await previous;

            return await operation();
        }
        finally
        {
            finished.SetResult();
        }
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
    /// Ends the game in progress: the session returns to a freshly registered campaign with the
    /// player at the world's start, and stops writing.
    /// </summary>
    /// <remarks>
    /// This is the line where one game ends and the next begins, so it is where writing stops. A
    /// write already asked for is not abandoned — it is progress the player earned, and the queue
    /// carries it through to the file — but no further write is queued on behalf of a game that is
    /// over. Whatever begins the next game waits its turn behind what is left, which is why
    /// <see cref="Continue"/> resets before it reads rather than after.
    /// </remarks>
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
