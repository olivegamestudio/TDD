using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Boundary cases for the save queue — added by QA while trying to break it, and kept as
/// regression coverage.
/// </summary>
/// <remarks>
/// <para>
/// The shipped tests establish the queue on the pair the issue reported: a new game's save and
/// the first frame's quest. These push on the parts that pair does not reach — a burst deeper
/// than two, what each queued write is carrying by the time its turn comes, and what a failure
/// part-way along the queue does to everything behind it, including a failure the session does
/// not classify as storage trouble and therefore never catches.
/// </para>
/// <para>
/// No culture is pinned: nothing here asserts on user-facing text. The payloads compared are
/// quest identifiers, quest states and coordinates, none of which are translated.
/// </para>
/// </remarks>
public sealed class GameSessionSaveOrderingAdversarialTests
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    static GameSession CreateSession(ISaveProgressService saves) =>
        new(saves,
            new TestCampaign(Quest("quest-1")),
            new TestWorld(new QuestMarkers("quest-1", Start, End)));

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            true);

    static SaveGame Read(string? content) =>
        SaveGameSerializer.Deserialize(content)
        ?? throw new InvalidOperationException("nothing readable was saved.");

    // ---- a queue deeper than the pair the issue reported --------------------

    [Fact]
    public async Task FiveSavesAskedForWhileStorageIsBusy_LandOneAtATime_InTheOrderAskedFor()
    {
        // The reported pair is two. Nothing about the defect stops a slow disk collecting a
        // handful — the campaign is one quest today, and the issue's whole point is that it will
        // not stay that way. A queue that orders two and not five would be no queue at all.
        GatedSaveService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();

        List<Task> asked = [];
        for (int step = 1; step <= 4; step++)
        {
            session.Player.MoveTo(new Position(step, 0));
            asked.Add(session.Save());
        }

        // storage has not answered, so exactly one of the five has begun
        Assert.Equal(1, saves.WritesBegun);

        saves.LetTheWritesFinish();
        await newGame;
        await Task.WhenAll(asked);

        Assert.Equal(5, saves.Landed.Count);
        Assert.Equal(1, saves.MostWritesInFlightAtOnce);
        Assert.Equal([0d, 1, 2, 3, 4], saves.Landed.Select(content => Read(content).PlayerX));
    }

    [Fact]
    public async Task EachQueuedWrite_CarriesTheSnapshotFromWhenItWasAskedFor()
    {
        // The stated reason the snapshot is taken at request time rather than at write time. If it
        // were taken when a write's turn came round, every queued write would land holding
        // whatever the game had reached by then — several copies of the newest state, and the
        // ordering guarantee would be describing something that no longer varies.
        GatedSaveService saves = new();
        GameSession session = CreateSession(saves);

        Task newGame = session.StartNewGame();

        session.Player.MoveTo(new Position(10, 20));
        Task first = session.Save();

        session.Player.MoveTo(new Position(30, 40));
        Task second = session.Save();

        // the player moves again *after* both saves were asked for and before either has been
        // written: neither queued write may pick this up
        session.Player.MoveTo(new Position(99, 99));

        saves.LetTheWritesFinish();
        await newGame;
        await first;
        await second;

        Assert.Equal(new Position(10, 20), PositionOf(saves.Landed[1]));
        Assert.Equal(new Position(30, 40), PositionOf(saves.Landed[2]));

        static Position PositionOf(string content)
        {
            SaveGame save = Read(content);
            return new Position(save.PlayerX, save.PlayerY);
        }
    }

    // ---- a failure part-way along the queue ---------------------------------

    [Fact]
    public async Task AWriteThatFailsInTheMiddle_DoesNotReorderTheWritesBehindIt()
    {
        // The shipped cover has the failure at the front of the queue. A failure in the middle is
        // the one that could plausibly reorder things, because the write behind it stops waiting
        // for a completion and starts waiting for a fault.
        FailingWriteSaveService saves = new(failWrite: 2);
        GameSession session = CreateSession(saves);

        await session.StartNewGame();

        session.Player.MoveTo(new Position(2, 0));
        Task failed = session.Save();

        session.Player.MoveTo(new Position(3, 0));
        Task after = session.Save();

        await Assert.ThrowsAsync<IOException>(() => failed);
        await after;

        // the second write threw, so only the first and third are on disk — in that order
        Assert.Equal([0d, 3], saves.Landed.Select(content => Read(content).PlayerX));
        Assert.Equal(3, Read(saves.Content).PlayerX);
    }

    [Fact]
    public async Task AWriteThatFailsWithSomethingOtherThanStorageTrouble_DoesNotWedgeTheQueue()
    {
        // TrySave only catches IOException and UnauthorizedAccessException — anything else is
        // treated as a defect and left to propagate. That means an autosave can fault in a way
        // nothing in the session catches, and the write queued behind it still has to run. The
        // guard is the bare catch in WriteAfter; without it a single unexpected exception would
        // stop the game saving for the rest of the run.
        FailingWriteSaveService saves = new(failWrite: 2, error: () => new InvalidOperationException("not storage."));
        GameSession session = CreateSession(saves);

        await session.StartNewGame();

        session.Quests.Find("quest-1")!.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PendingSave);

        session.Quests.Find("quest-1")!.Complete();
        await session.PendingSave;

        Assert.Equal(QuestState.Completed, Read(saves.Content).Quests.Single().State);
        Assert.Equal(3, saves.WritesBegun);
    }

    [Fact]
    public async Task PendingSave_CompletesOnlyAfterAnEarlierWriteHasFinished_EvenWhenItFailed()
    {
        // PendingSave's doc comment says there is no state of affairs in which it has completed
        // and an earlier save has not. A write that *failed* has still finished, so the guarantee
        // has to hold across one — otherwise a shutdown path awaiting PendingSave could return
        // while a failed write was still unwinding.
        FailingWriteSaveService saves = new(failWrite: 1);
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        Assert.NotNull(session.SaveError);

        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.Equal(2, saves.Finished);
        Assert.Equal(QuestState.Active, Read(saves.Content).Quests.Single().State);
    }

    // ---- the queue must not cost anything when it is empty ------------------

    [Fact]
    public void ASaveAskedForWhenNothingIsOutstanding_DoesNotWaitForAnything()
    {
        // Storage that answers immediately must not be made asynchronous by the queue. Every
        // automatic save goes through here on the frame loop, so a save that yields when it did
        // not have to would be a cost paid on every quest transition for nothing.
        InMemorySaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        Task save = session.Save();

        Assert.True(save.IsCompletedSuccessfully);
    }

    // ---- fakes ----

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Start;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    /// <summary>
    /// Storage that answers only when it is told to, recording how many writes were ever
    /// outstanding together and every payload in the order it landed.
    /// </summary>
    sealed class GatedSaveService : ISaveProgressService
    {
        readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        int _inFlight;

        public string? Content { get; private set; }

        public List<string> Landed { get; } = [];

        public int WritesBegun { get; private set; }

        public int MostWritesInFlightAtOnce { get; private set; }

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
    /// Storage whose nth write fails and whose other writes succeed, so a queue can be watched
    /// surviving a failure anywhere along it rather than only at the front.
    /// </summary>
    /// <param name="failWrite">Which write to fail, counting from one.</param>
    /// <param name="error">
    /// What it fails with. Defaults to the <see cref="IOException"/> a locked file throws; a
    /// caller can supply something the session does not classify as storage trouble.
    /// </param>
    sealed class FailingWriteSaveService(int failWrite, Func<Exception>? error = null)
        : ISaveProgressService
    {
        public string? Content { get; private set; }

        public List<string> Landed { get; } = [];

        public int WritesBegun { get; private set; }

        /// <summary>How many writes have run to a conclusion, whether they wrote or threw.</summary>
        public int Finished { get; private set; }

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load() => Task.FromResult(Content);

        public async Task Save(string content)
        {
            int write = ++WritesBegun;

            // yielded, so the write is genuinely outstanding rather than finishing before the
            // caller has had a chance to queue anything behind it
            await Task.Yield();

            if (write == failWrite)
            {
                Finished++;
                throw error?.Invoke() ?? new IOException("the save file is in use.");
            }

            Content = content;
            Landed.Add(content);
            Finished++;
        }

        public Task SetAside() => Task.CompletedTask;
    }
}
