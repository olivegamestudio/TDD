using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Cover for the order the session writes its save in. Every test here drives storage that does
/// not answer within a frame, which is the condition the defect needs and which
/// <see cref="InMemorySaveProgressService"/> — completing synchronously — can never reproduce.
/// </summary>
/// <remarks>
/// Nothing here asserts on user-facing text, so no culture is pinned: the payloads compared are
/// serialised quest identifiers and states, which are never translated.
/// </remarks>
public sealed class GameSessionSaveOrderingTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Start;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            true);

    static GameSession CreateSession(ISaveProgressService saves, params string[] questIds)
    {
        string[] ids = questIds.Length == 0 ? ["quest-1"] : questIds;

        return new GameSession(
            saves,
            new TestCampaign([.. ids.Select(Quest)]),
            new TestWorld([.. ids.Select(id => new QuestMarkers(id, Start, End))]));
    }

    static QuestState StateOf(string? content, string questId) =>
        (SaveGameSerializer.Deserialize(content)
            ?? throw new InvalidOperationException("nothing readable was saved."))
        .Quests.Single(quest => quest.QuestId == questId).State;

    // ---- two writes are never outstanding together ----

    [Fact]
    public async Task ANewGame_DoesNotHaveTwoSavesInFlightAtOnce()
    {
        // StartNewGame marks the session ready and only then awaits its first save, so on storage
        // that does not answer within a frame the update loop is already running while that write
        // is outstanding. The first frame starts quest 1, which asks for a second write of the
        // same file. Nothing but the session can order those two, because only the session knows
        // which snapshot is the newer.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();

        // the new game's own save is outstanding: storage has not answered yet
        Assert.False(newGame.IsCompleted);
        Assert.Equal(1, saves.WritesBegun);

        session.Quests.Find("quest-1")!.Start();

        // the quest's save is queued behind it rather than issued alongside it
        Assert.Equal(1, saves.WritesBegun);

        saves.LetTheWritesFinish();
        await newGame;
        await session.PendingSave;

        Assert.Equal(2, saves.WritesBegun);
        Assert.Equal(1, saves.MostWritesInFlightAtOnce);
    }

    [Fact]
    public async Task TheSaveOnDisk_IsTheNewestSnapshotTaken_NotWhicheverWriteFinishedLast()
    {
        // The consequence of two writes being outstanding together: nothing orders them, so the
        // older snapshot — taken before quest 1 started — is free to land after the newer one and
        // become the save. The player is then handed back a game in which the opening quest never
        // began, with no file corrupted and no error raised to say so.
        ReversingSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();

        await saves.AnswerEveryWriteNewestFirst(writes: 2);
        await newGame;
        await session.PendingSave;

        Assert.Equal(QuestState.Active, StateOf(saves.Content, "quest-1"));
        Assert.Null(session.SaveError);
    }

    [Fact]
    public async Task WritesLand_InTheOrderTheirSnapshotsWereTaken()
    {
        // Not just "the newest wins" — each write carries the state as it stood when that save was
        // asked for, and they reach storage in that order. A save that arrived later holding older
        // progress would be the same defect wearing a different result.
        ReversingSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();
        session.Quests.Find("quest-1")!.Complete();

        await saves.AnswerEveryWriteNewestFirst(writes: 3);
        await newGame;
        await session.PendingSave;

        Assert.Equal(
            [QuestState.NotStarted, QuestState.Active, QuestState.Completed],
            saves.Landed.Select(content => StateOf(content, "quest-1")));
    }

    [Fact]
    public async Task ASaveAskedForDirectly_IsOrderedWithTheAutomaticOnes()
    {
        // Save() is what a shutdown flush would call, and it is the caller most likely to run
        // while an autosave is still outstanding.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();
        Task flush = session.Save();

        Assert.Equal(1, saves.WritesBegun);

        saves.LetTheWritesFinish();
        await newGame;
        await flush;

        Assert.Equal(2, saves.WritesBegun);
        Assert.Equal(1, saves.MostWritesInFlightAtOnce);
    }

    [Fact]
    public async Task PendingSave_CompletesOnlyOnceEveryWriteQueuedBeforeItHas()
    {
        // PendingSave is documented as what a caller awaits when it needs the write to have
        // landed. With writes queued rather than issued together, that has to mean the whole
        // queue up to it and not merely the last one to be asked for.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();

        Assert.False(session.PendingSave.IsCompleted);

        saves.LetTheWritesFinish();
        await session.PendingSave;

        // both landed, not merely the one PendingSave was handed
        Assert.Equal(2, saves.Landed.Count);
        Assert.Equal(QuestState.Active, StateOf(saves.Content, "quest-1"));

        await newGame;
    }

    // ---- a write that fails does not take the queue with it ----

    [Fact]
    public async Task AWriteThatFails_DoesNotStopTheNextOneFromRunning()
    {
        // Saving is deliberately left on after a storage failure, because the file may be free
        // again by the next quest. Queueing writes must not quietly undo that by refusing to run
        // anything behind a write that threw.
        FailingThenHealthySaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        Assert.NotNull(session.SaveError);

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.Null(session.SaveError);
        Assert.Equal(QuestState.Active, StateOf(saves.Content, "quest-1"));
    }

    [Fact]
    public async Task AWriteThatFails_IsNotReportedToTheCallerBehindIt()
    {
        // Save() raises storage failures to its caller. Queueing must not hand one caller the
        // failure of a write it never asked for — a shutdown flush reporting an autosave's error
        // would send whoever is listening after the wrong problem.
        FailingThenHealthySaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();

        // the flush must complete on its own merits: raising here would be the first write's
        // IOException handed to a caller that never asked for that write
        await session.Save();

        Assert.Equal(2, saves.WritesBegun);
        Assert.NotNull(saves.Content);
    }

    /// <summary>
    /// A save service that answers only when it is told to, standing in for storage that takes
    /// longer than a frame — which every real disk sometimes does. It records how many writes were
    /// ever outstanding together, which is the thing a single file cannot survive, and every
    /// payload in the order it landed.
    /// </summary>
    sealed class GatedSaveProgressService : ISaveProgressService
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        int _inFlight;

        /// <summary>Gets the content last written.</summary>
        public string? Content { get; private set; }

        /// <summary>Gets every payload written, in the order it landed.</summary>
        public List<string> Landed { get; } = [];

        /// <summary>Gets how many writes have been started.</summary>
        public int WritesBegun { get; private set; }

        /// <summary>Gets the greatest number of writes that were ever outstanding together.</summary>
        public int MostWritesInFlightAtOnce { get; private set; }

        /// <summary>Answers every write that is waiting, and every write that comes after.</summary>
        public void LetTheWritesFinish() => _released.TrySetResult();

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() => Task.FromResult(Content);

        public async Task Save(string content)
        {
            WritesBegun++;
            _inFlight++;
            MostWritesInFlightAtOnce = Math.Max(MostWritesInFlightAtOnce, _inFlight);

            await _released.Task;

            Content = content;
            Landed.Add(content);
            _inFlight--;
        }

        public Task SetAside() => Task.CompletedTask;
    }

    /// <summary>
    /// A save service that answers whichever write began most recently first, standing in for
    /// storage that does not finish writes in the order it was given them — which nothing
    /// anywhere promises it will.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes are answered one at a time and each is waited out before the next is touched, so
    /// what this shows is a fixed sequence rather than a race. The same loop drives both worlds:
    /// with two writes outstanding together it answers the newer one first and the older one then
    /// lands on top of it, and with writes queued one at a time there is only ever one to answer
    /// and the one behind it begins as that one finishes.
    /// </para>
    /// </remarks>
    sealed class ReversingSaveProgressService : ISaveProgressService
    {
        /// <summary>
        /// How long to wait for a write that was expected and has not arrived, so a session that
        /// queues a write and never issues it fails the test rather than hanging it.
        /// </summary>
        static readonly TimeSpan LongEnoughThatNothingIsComing = TimeSpan.FromSeconds(10);

        readonly List<(TaskCompletionSource Gate, TaskCompletionSource Done)> _writes = [];

        /// <summary>
        /// Signals that a write has begun. Replaced each time it is waited on, because a write
        /// queued behind another only begins once that one has finished — so the loop below has to
        /// be told, not left to guess when to look again.
        /// </summary>
        TaskCompletionSource _begun = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the content last written.</summary>
        public string? Content { get; private set; }

        /// <summary>Gets every payload written, in the order it landed.</summary>
        public List<string> Landed { get; } = [];

        /// <summary>
        /// Answers writes until <paramref name="writes"/> of them have landed, always taking the
        /// one that began most recently first.
        /// </summary>
        /// <param name="writes">How many writes the session is expected to make.</param>
        public async Task AnswerEveryWriteNewestFirst(int writes)
        {
            while (Landed.Count < writes)
            {
                int newest = Waiting();
                if (newest < 0)
                {
                    Task begun = ArmTheArrivalSignal();

                    // re-checked after arming, so a write that began in between is not missed
                    newest = Waiting();
                    if (newest < 0)
                    {
                        await begun.WaitAsync(LongEnoughThatNothingIsComing);
                        continue;
                    }
                }

                _writes[newest].Gate.TrySetResult();

                // waited out rather than merely released, so the next write cannot start landing
                // while this one is still doing so
                await _writes[newest].Done.Task.WaitAsync(LongEnoughThatNothingIsComing);
            }
        }

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() => Task.FromResult(Content);

        public async Task Save(string content)
        {
            TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _writes.Add((gate, done));
            _begun.TrySetResult();

            await gate.Task;

            Content = content;
            Landed.Add(content);
            done.TrySetResult();
        }

        public Task SetAside() => Task.CompletedTask;

        /// <summary>The index of the newest write still waiting to be answered, or -1.</summary>
        int Waiting() => _writes.FindLastIndex(write => !write.Gate.Task.IsCompleted);

        /// <summary>A fresh signal for the next write to begin, and the task to wait on.</summary>
        Task ArmTheArrivalSignal()
        {
            _begun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _begun.Task;
        }
    }

    /// <summary>
    /// A save service whose first write fails the way a locked file does and whose later writes
    /// succeed, so a queue can be watched surviving a failure in front of it.
    /// </summary>
    sealed class FailingThenHealthySaveProgressService : ISaveProgressService
    {
        /// <summary>Gets the content last written.</summary>
        public string? Content { get; private set; }

        /// <summary>Gets how many writes have been started.</summary>
        public int WritesBegun { get; private set; }

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() => Task.FromResult(Content);

        public async Task Save(string content)
        {
            WritesBegun++;

            // yield, so the write is genuinely outstanding rather than throwing before the caller
            // has had a chance to queue anything behind it
            await Task.Yield();

            if (WritesBegun == 1)
            {
                throw new IOException("the save file is in use.");
            }

            Content = content;
        }

        public Task SetAside() => Task.CompletedTask;
    }
}
