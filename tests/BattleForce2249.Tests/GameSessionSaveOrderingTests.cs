using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// The order the session writes in. Every test here drives storage that does not answer within a
/// frame, which is the condition the defect needs and which
/// <see cref="InMemorySaveProgressService"/> — completing synchronously — can never reproduce.
/// </summary>
/// <remarks>
/// No culture is pinned, deliberately: nothing here asserts on user-facing text. What is compared
/// is quest identifiers and states, which are never translated.
/// </remarks>
public sealed class GameSessionSaveOrderingTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    const string QuestId = "quest-1";

    /// <summary>
    /// A save this build will not read, so <see cref="GameSession.Continue"/> sets it aside and
    /// starts a new game over it.
    /// </summary>
    const string RefusedSave = "{ not a save at all";

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Start;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    static GameSession CreateSession(ISaveProgressService saves) =>
        new(saves,
            new TestCampaign(new QuestDefinition(
                QuestId,
                "Title of quest-1",
                new QuestTrigger(QuestTriggerKind.Proximity, 25),
                new QuestTrigger(QuestTriggerKind.Proximity, 50),
                true)),
            new TestWorld(new QuestMarkers(QuestId, Start, End)));

    static QuestState StateIn(string? content) =>
        SaveGameSerializer.Deserialize(content)?.Quests.Single().State
            ?? throw new InvalidOperationException("nothing readable was saved.");

    // ---- two writes are never outstanding together ----

    [Fact]
    public async Task ANewGame_DoesNotHaveTwoSavesInFlightAtOnce()
    {
        // StartNewGame marks the session ready and only then awaits its first save, so on any
        // storage that does not answer within a frame the update loop is already running while that
        // write is outstanding. The first frame puts the player inside quest 1's start trigger, the
        // quest starts, and that writes the same file again. Nothing held the first write before
        // the second began.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Assert.Equal(1, saves.WritesBegun);

        session.Quests.Find(QuestId)!.Start();
        Task pending = session.PendingSave;

        saves.LetTheWritesFinish();
        await started;
        await pending;

        Assert.Equal(2, saves.WritesBegun);
        Assert.Equal(1, saves.MostWritesInFlightAtOnce);
    }

    [Fact]
    public async Task TheSaveOnDisk_IsTheNewestSnapshotTaken_NotWhicheverWriteFinishedLast()
    {
        // The consequence of two writes being outstanding together. Nothing ordered them, so the
        // older snapshot — taken before quest 1 started — was free to land after the newer one and
        // become the save. The player is then handed back a game in which the opening quest never
        // began, and no file is corrupted and no error raised to say so.
        WritesLandNewestFirstSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        session.Quests.Find(QuestId)!.Start();
        Task pending = session.PendingSave;

        saves.LetTheWritesFinish();
        await started;
        await pending;

        Assert.Equal(QuestState.Active, StateIn(saves.Content));
    }

    [Fact]
    public async Task WritesLand_InTheOrderTheirSnapshotsWereTaken()
    {
        // Not merely the last one: every write lands where it was asked for in the sequence, so no
        // intermediate state of the campaign is ever the file's most recent content out of turn.
        WritesLandNewestFirstSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Quest quest = session.Quests.Find(QuestId)!;
        quest.Start();
        Task afterStart = session.PendingSave;
        quest.Complete();
        Task afterComplete = session.PendingSave;

        saves.LetTheWritesFinish();
        await started;
        await afterStart;
        await afterComplete;

        Assert.Equal(
            [QuestState.NotStarted, QuestState.Active, QuestState.Completed],
            saves.Written.Select(StateIn));
    }

    [Fact]
    public async Task ASaveAskedForDirectly_IsOrderedWithTheAutomaticOnes()
    {
        // Save() is the shutdown flush. It is a snapshot like any other and belongs in the same
        // queue; a flush that overtook the autosaves would write an older game over a newer one at
        // exactly the moment nothing is left to correct it.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Task direct = session.Save();

        Assert.Equal(1, saves.WritesBegun);

        saves.LetTheWritesFinish();
        await started;
        await direct;

        Assert.Equal(1, saves.MostWritesInFlightAtOnce);
    }

    [Fact]
    public async Task EachQueuedWrite_CarriesTheSnapshotFromWhenItWasAskedFor()
    {
        // A queued write has to carry the progress that prompted it, or the queue is just several
        // writes of whatever the game happened to reach by the time each ran. Regression cover: the
        // snapshot was already taken at request time before the queue, and must stay there.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();

        session.Player.MoveTo(new Position(0, 100));
        session.Quests.Find(QuestId)!.Start();
        Task pending = session.PendingSave;

        // and on again, before the write it prompted has had a chance to run
        session.Player.MoveTo(new Position(0, 900));

        saves.LetTheWritesFinish();
        await started;
        await pending;

        SaveGame written = SaveGameSerializer.Deserialize(saves.Written[1])!;
        Assert.Equal(100, written.PlayerY);
    }

    [Fact]
    public async Task PendingSave_CompletesOnlyOnceEveryWriteQueuedBeforeItHas()
    {
        // Its doc comment is what a shutdown path relies on. There must be no state of affairs in
        // which it has completed and a save asked for earlier has not.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        session.Quests.Find(QuestId)!.Start();
        Task pending = session.PendingSave;

        Assert.False(pending.IsCompleted);

        saves.LetTheWritesFinish();
        await pending;

        Assert.Equal(2, saves.Written.Count);
    }

    // ---- a write that fails ----

    [Fact]
    public async Task AWriteThatFails_DoesNotStopTheNextOneFromRunning()
    {
        // A game that gave up on saving after one bad moment would lose far more than it protects:
        // the file may be free again by the next quest. Regression cover for the queue — one
        // failure must not wedge everything behind it.
        HeldSaveProgressService saves = new() { FailWriteAt = 0 };
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        session.Quests.Find(QuestId)!.Start();
        Task pending = session.PendingSave;

        saves.LetTheWritesFinish();
        await started;
        await pending;

        Assert.Equal(QuestState.Active, StateIn(saves.Content));
        Assert.True(session.IsSavingProgress);
    }

    [Fact]
    public async Task AWriteThatFails_IsNotReportedToTheCallerBehindIt()
    {
        // A failure belongs to the caller who asked for that write and to nobody else. Save() the
        // shutdown flush must not raise because an autosave two quests ago found the file locked.
        HeldSaveProgressService saves = new() { FailWriteAt = 0 };
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Task direct = session.Save();

        saves.LetTheWritesFinish();
        await started;
        await direct;

        Assert.NotNull(session.SaveError);
    }

    [Fact]
    public async Task AWriteThatFails_IsRaisedToItsOwnCaller()
    {
        // Save() raises where TrySave records, and the queue must not swallow that on the way.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        saves.FailWriteAt = 1;
        Task direct = session.Save();

        saves.LetTheWritesFinish();
        await started;

        await Assert.ThrowsAsync<IOException>(() => direct);
    }

    // ---- the boundary between two games ----

    [Fact]
    public async Task AWriteOutstandingWhenTheNextGameBegins_LandsBeforeTheNextGameReadsTheFile()
    {
        // IGameSession is a singleton, so one save queue serves every entry into the game screen.
        // A write queued by the game before must not still be to come when the next one reads the
        // file: it would land on top of what was just resumed, putting the previous game's snapshot
        // over it. The queue orders writes against each other; it does not know that the game they
        // belonged to is over, so the boundary is where that is said.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Task resumed = session.Continue();

        saves.LetTheWritesFinish();
        await started;
        await resumed;

        Assert.Equal(["save", "load"], saves.Operations);
    }

    [Fact]
    public async Task TheGameTheFileHolds_IsTheGameThePlayerIsIn_AcrossAGameBoundary()
    {
        // What the ordering above is for. Whatever game the session ends up in, the file agrees
        // with it — a leftover write cannot leave the player playing one campaign while the disk
        // holds another.
        HeldSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task started = session.StartNewGame();
        Quest quest = session.Quests.Find(QuestId)!;
        quest.Start();
        quest.Complete();
        Task pending = session.PendingSave;

        Task resumed = session.Continue();

        saves.LetTheWritesFinish();
        await started;
        await pending;
        await resumed;

        Assert.Equal(session.Quests.Find(QuestId)!.State, StateIn(saves.Content));
    }

    [Fact]
    public async Task ASetAside_IsInTheSameQueueAsTheWrites()
    {
        // Moving a refused save out of the way before writing the new game over it is an ordering
        // guarantee, and it is worth exactly as much as the ordering underneath it. A write that
        // overtook the move would recreate a save file behind it, so the file the player was told
        // had been kept is no longer the only copy.
        HeldSaveProgressService saves = new() { Content = RefusedSave };
        GameSession session = CreateSession(saves);

        Task resumed = session.Continue();
        saves.LetTheWritesFinish();
        await resumed;

        Assert.Equal(["load", "set-aside", "save"], saves.Operations);
        Assert.Equal(RefusedSave, saves.SetAsideContent);
    }

    [Fact]
    public async Task ARefusedSaveThatCannotBeMoved_IsStillPlayedOverWithSavingHeldBack()
    {
        // Unchanged by the queue, and the thing most at risk from putting SetAside in it: the
        // failure still has to reach StartNewGameOver, or a save that could not be moved would be
        // written over instead of preserved.
        HeldSaveProgressService saves = new() { Content = RefusedSave, FailTheSetAside = true };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.False(session.IsSavingProgress);
        Assert.NotNull(session.SaveError);
        Assert.Equal(RefusedSave, saves.Content);
    }

    // ---- the storage these tests are driven against ----

    /// <summary>
    /// Storage that answers only when it is told to, standing in for a disk that takes longer than
    /// a frame — which every real one sometimes does. It records how many writes were ever
    /// outstanding together, which is the thing a single file cannot survive, and the order in
    /// which it finished what it was asked to do.
    /// </summary>
    sealed class HeldSaveProgressService : ISaveProgressService
    {
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly List<string> _operations = [];
        readonly List<string> _written = [];
        int _inFlight;

        /// <summary>Gets or sets what is on disk.</summary>
        public string? Content { get; set; }

        /// <summary>Gets what was moved out of the way, or <c>null</c> if nothing was.</summary>
        public string? SetAsideContent { get; private set; }

        /// <summary>Gets what the service was asked to do, in the order it finished doing it.</summary>
        public IReadOnlyList<string> Operations => _operations;

        /// <summary>Gets the content of every write that landed, in the order it landed.</summary>
        public IReadOnlyList<string> Written => _written;

        /// <summary>Gets how many writes have been started.</summary>
        public int WritesBegun { get; private set; }

        /// <summary>Gets the greatest number of writes ever outstanding together.</summary>
        public int MostWritesInFlightAtOnce { get; private set; }

        /// <summary>
        /// Gets or sets which write fails, counting from the first, or <c>null</c> for none. It
        /// fails the way a locked file does, which is what the session classifies as storage
        /// getting in the way rather than as a defect.
        /// </summary>
        public int? FailWriteAt { get; set; }

        /// <summary>Gets or sets whether the set-aside fails, standing in for a file that cannot be moved.</summary>
        public bool FailTheSetAside { get; set; }

        /// <summary>Answers every write that is waiting, and every write that comes after.</summary>
        public void LetTheWritesFinish() => _released.TrySetResult();

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load()
        {
            _operations.Add("load");
            return Task.FromResult(Content);
        }

        public async Task Save(string content)
        {
            int position = WritesBegun++;
            _inFlight++;
            MostWritesInFlightAtOnce = Math.Max(MostWritesInFlightAtOnce, _inFlight);

            await _released.Task;

            _inFlight--;

            if (position == FailWriteAt)
            {
                _operations.Add("failed save");
                throw new IOException("the save file is in use.");
            }

            Content = content;
            _written.Add(content);
            _operations.Add("save");
        }

        public Task SetAside()
        {
            if (FailTheSetAside)
            {
                _operations.Add("failed set-aside");
                throw new IOException("the save file cannot be moved.");
            }

            SetAsideContent = Content;
            Content = null;
            _operations.Add("set-aside");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Storage that does not finish writes in the order it was handed them: a write is applied only
    /// once every write begun after it has been. Nothing anywhere promises otherwise, which is the
    /// point — a caller that leaves two writes outstanding has no claim on which of them survives.
    /// </summary>
    sealed class WritesLandNewestFirstSaveProgressService : ISaveProgressService
    {
        readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly List<TaskCompletionSource> _landed = [];
        readonly List<string> _written = [];

        /// <summary>Gets what is on disk.</summary>
        public string? Content { get; private set; }

        /// <summary>Gets the content of every write that landed, in the order it landed.</summary>
        public IReadOnlyList<string> Written => _written;

        /// <summary>Answers every write that is waiting, and every write that comes after.</summary>
        public void LetTheWritesFinish() => _released.TrySetResult();

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() => Task.FromResult(Content);

        public Task SetAside() => Task.CompletedTask;

        public async Task Save(string content)
        {
            int position = _landed.Count;
            TaskCompletionSource landed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _landed.Add(landed);

            await _released.Task;

            // every write begun after this one lands before it does. With two outstanding at once
            // that reverses them; with one at a time there is never a later one to wait for, so the
            // order is untouched.
            for (int later = _landed.Count - 1; later > position; later--)
            {
                await _landed[later].Task;
            }

            Content = content;
            _written.Add(content);
            landed.TrySetResult();
        }
    }
}
