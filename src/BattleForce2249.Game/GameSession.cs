using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The default <see cref="IGameSession"/>. It holds the player and the quest log, and persists both
/// through the engine's save progress service whenever a quest starts or completes.
/// </summary>
public sealed class GameSession : IGameSession
{
    readonly ISaveProgressService _saveProgressService;
    readonly ICampaign _campaign;
    readonly IWorld _world;

    /// <summary>
    /// Guards <see cref="_queue"/>. Queueing is two steps — read the tail, then replace it — and
    /// they have to be one, or two callers can both queue behind the same write and run together.
    /// </summary>
    readonly Lock _queueLock = new();

    /// <summary>
    /// Everything the session has asked the save service to do, chained end to end. A new piece of
    /// work is put behind this rather than started beside it, so the service is never asked to do
    /// two things at once and the order it is asked in is the order they happen in.
    /// </summary>
    /// <remarks>
    /// Only the caller knows which of two snapshots is the newer — the service is handed two pieces
    /// of text and has no way to tell them apart — so ordering them is the session's job. Not
    /// tearing the file when two callers overlap anyway is the service's, and
    /// <see cref="ISaveProgressService.Save"/> says so.
    /// </remarks>
    Task _queue = Task.CompletedTask;

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
        await LetTheGameBeforeFinishWriting();

        Reset();

        _autoSave = true;
        IsReady = true;

        await TrySave();
    }

    /// <inheritdoc />
    public async Task Continue()
    {
        SaveError = null;

        await LetTheGameBeforeFinishWriting();

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
        Quests.Restore(save.Quests);

        // Reset has already put the player where a new game begins, which is where a campaign
        // nobody has started belongs. Moving them is what resuming a game in progress means.
        if (HasBegun)
        {
            Player.MoveTo(new Position(save.PlayerX, save.PlayerY));
        }

        _autoSave = true;
        IsReady = true;
    }

    /// <summary>
    /// Whether the campaign restored from a save has actually been played — at least one quest the
    /// campaign ships came back started or finished.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It decides whether a saved position is used, because a position is only progress beside the
    /// quest progress it was taken with. Reading a save is deliberately forgiving about quests: an
    /// entry naming a quest this build no longer ships is ignored, and so is one naming no quest at
    /// all. A file whose every entry is drift is therefore perfectly readable and restores nothing,
    /// so the quest log comes back exactly as a new game's while the coordinates put the player
    /// somewhere a new game never starts.
    /// </para>
    /// <para>
    /// That combination strands them. Quest 1 begins within 25 units of a marker a new game spawns
    /// the player on, so a player set down 700 units out has no quest active, nothing to fly
    /// towards, and gets further from the only trigger that could help with every frame of flying
    /// forward — the one direction the game teaches. The game is not frozen, but the only way out
    /// of it is backwards, through the debris field quest 1 is about escaping.
    /// </para>
    /// <para>
    /// The file is still read rather than refused, and that is the cheaper mistake of the two. A
    /// refused save is set aside and replaced by the new game written over it, so the player loses
    /// the file as well as the position. Declining only the position leaves the file where it is
    /// until real progress is made and written over it.
    /// </para>
    /// <para>
    /// What the line costs is a save taken after the player has travelled but before any quest has
    /// begun: resuming it sends them back to the start. No such save can be written today —
    /// progress is captured when a quest changes state, and quest 1 begins on the starting marker —
    /// but a campaign whose first quest does not start where the player spawns would make one, and
    /// this needs revisiting with it.
    /// </para>
    /// </remarks>
    bool HasBegun => Quests.Quests.Any(quest => quest.State is not QuestState.NotStarted);

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
                await Queue(() => _saveProgressService.SetAside());
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
        // Snapshotted here rather than when the write runs. A queued write has to carry the
        // progress that prompted it, or the queue is just several writes of whatever the game
        // happened to reach by the time each one's turn came round.
        //
        // Taken outside the queue's lock, which is safe because the session is driven from one
        // thread: the frame loop starts and completes quests, and Capture reads the player and the
        // quest log, neither of which is thread-safe against it anyway. Two threads asking to save
        // at once could snapshot in one order and queue in the other — but a session that could be
        // driven from two threads has larger problems than the order of its writes, and locking
        // here would hide that rather than fix it.
        string content = SaveGameSerializer.Serialize(Capture());

        return Queue(() => _saveProgressService.Save(content));
    }

    /// <summary>
    /// Puts a piece of save work behind everything the session has already asked for, and hands
    /// back a task completing when that work — and so everything before it — has finished.
    /// </summary>
    /// <remarks>
    /// A failure belongs to the caller who asked for that work and to nobody else. The queue itself
    /// carries on: one write failing because the file was locked for a moment must not stop the
    /// game saving for the rest of the run, which is why what is chained on is the swallowing task
    /// rather than the one handed back.
    /// </remarks>
    /// <param name="work">What to do once everything queued before it has been done.</param>
    /// <returns>A task completing when that work has finished, faulted if it failed.</returns>
    Task Queue(Func<Task> work)
    {
        lock (_queueLock)
        {
            Task queued = RunAfter(_queue, work);
            _queue = Swallow(queued);
            return queued;
        }
    }

    /// <summary>
    /// Runs work once the task before it has finished, however it finished.
    /// </summary>
    static async Task RunAfter(Task previous, Func<Task> work)
    {
        await previous;
        await work();
    }

    /// <summary>
    /// The same task, complete rather than faulted. It is what the next piece of work waits on, so
    /// that a failure is reported to its own caller once and not to everyone behind it.
    /// </summary>
    static async Task Swallow(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // deliberately everything: the queue's job is to keep running, and whatever this was it
            // has already been handed to the caller who asked for the work.
        }
    }

    /// <summary>
    /// Waits for every save the session has already asked for, so that one game's writes are all
    /// behind it before the next game reads or replaces the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IGameSession"/> is a singleton, so one session — and one queue — serves every
    /// entry into the game screen. The queue orders writes against each other; it has no idea that
    /// the game which asked for one is over. Without this, a write left over from the previous game
    /// lands after the next one has resumed and puts the older snapshot back over it, and nothing
    /// anywhere reports it.
    /// </para>
    /// <para>
    /// It is a wait that is normally already over: on the first entry there is nothing queued, and
    /// on a later one the writes finished while the player was in the menu. Entering the game
    /// screen is already off the frame loop, so what it costs is a moment on a screen the player is
    /// waiting on anyway — and what it buys is that the file and the game they are handed agree.
    /// </para>
    /// </remarks>
    Task LetTheGameBeforeFinishWriting() => Queue(() => Task.CompletedTask);

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
