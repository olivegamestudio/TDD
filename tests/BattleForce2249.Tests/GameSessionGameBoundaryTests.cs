using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Cover for the boundary between two games played through one session (#85). The queue #63 added
/// orders the writes <em>within</em> a game; these are the cases that survive it, where an
/// operation left over from the game before meets the game after.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IGameSession"/> is registered as a singleton, so one session and one queue serve
/// every entry into the game screen. Nothing here is reachable in the shipping game today —
/// <c>Continue</c> has one caller and there is no way out of the game screen and back again — so
/// every test drives the session directly, which is the seam the defect lives at.
/// </para>
/// <para>
/// The storage double answers reads at once and takes longer than a frame over writes, which is
/// what makes these fixed sequences rather than races: a read that is ordered against an
/// outstanding write cannot answer, and a read that is not ordered against it answers immediately
/// with whatever the write has not yet changed.
/// </para>
/// <para>
/// Nothing here asserts on user-facing text, so no culture is pinned: the payloads compared are
/// serialised quest identifiers and states, which are never translated.
/// </para>
/// </remarks>
public sealed class GameSessionGameBoundaryTests
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

    // ---- a write left over from a finished game meets the game after ----

    [Fact]
    public async Task AWriteQueuedByOneGame_CannotLandAfterALaterGameHasResumedFromTheFile()
    {
        // The reported defect. A write the previous game asked for is still outstanding when the
        // next game reads the file, so the read misses it and the write then lands on top of what
        // was resumed. The player is handed a game the file does not hold, and the next quest to
        // change writes that older game back out over their real progress. Nothing is corrupted
        // and nothing is raised to say so.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        // the game reaches its objective, and storage stops answering before that write lands
        saves.HoldTheWritesFromNowOn();
        session.Quests.Find("quest-1")!.Complete();
        Task leftOver = session.PendingSave;

        Task resuming = session.Continue();
        saves.LetTheWritesFinish();
        await resuming;
        await leftOver;

        Assert.True(session.IsReady);
        Assert.Equal(
            StateOf(saves.Content, "quest-1"),
            session.Quests.Find("quest-1")!.State);
    }

    [Fact]
    public async Task AWriteQueuedByAFinishedGame_IsStillWritten()
    {
        // The other half of the same choice, and the reason the outstanding write is waited out
        // rather than abandoned: a write that was asked for is progress the player earned.
        // Dropping it would be cheaper, and would satisfy the test above just as well by leaving
        // the file untouched — while quietly costing the player the last quest they finished.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        saves.HoldTheWritesFromNowOn();
        session.Quests.Find("quest-1")!.Complete();
        Task leftOver = session.PendingSave;

        Task resuming = session.Continue();
        saves.LetTheWritesFinish();
        await resuming;
        await leftOver;

        Assert.Equal(QuestState.Completed, StateOf(saves.Content, "quest-1"));
        Assert.Equal(QuestState.Completed, session.Quests.Find("quest-1")!.State);
    }

    [Fact]
    public async Task TheReadOfTheSave_IsNeverOverlappedByAWrite()
    {
        // The ordering above stated as the property rather than as one of its outcomes: the
        // session never has two things going at the same file at once, whichever they are.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        saves.HoldTheWritesFromNowOn();
        session.Quests.Find("quest-1")!.Complete();
        Task leftOver = session.PendingSave;

        Task resuming = session.Continue();
        saves.LetTheWritesFinish();
        await resuming;
        await leftOver;

        Assert.Equal(1, saves.MostOperationsInFlightAtOnce);
    }

    [Fact]
    public async Task WhileTheSaveIsBeingRead_TheSessionIsNoLongerSavingTheGameItIsLeaving()
    {
        // Where the boundary between two games is. Autosave used to stay on until the read
        // returned, so a frame arriving in that window queued exactly the leftover write the read
        // had just been ordered against — the game being left could still write while the game
        // arriving was reading.
        GatedSaveProgressService saves = new();
        GameSession session = CreateSession(saves);

        await session.StartNewGame();
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        bool stillSaving = true;
        saves.WhenLoadBegins = () => stillSaving = session.IsSavingProgress;

        await session.Continue();

        Assert.False(stillSaving);
        Assert.True(session.IsSavingProgress);
    }

    // ---- setting a refused save aside, against a write that is still to come ----

    [Fact]
    public async Task ALeftOverWrite_CannotOverlapASetAside()
    {
        // #61's guarantee is that a refused save is moved out of the way rather than written over,
        // so a later build could still read it. A write the session had already been asked for and
        // had not yet run was ordered against that move by nothing at all, so it could land after
        // it and recreate a save file behind it — leaving the file that was set aside no longer
        // the only copy, and the new game's first save no longer the first thing written.
        //
        // The write is asked for while the read is in flight, which is the window that ending the
        // previous game first cannot close, and the reason the move is queued rather than merely
        // preceded by a wait.
        GatedSaveProgressService saves = new() { Content = "not a save this build can read" };
        GameSession session = CreateSession(saves);

        Task? flush = null;
        saves.WhenLoadBegins = () => flush = session.Save();

        saves.HoldTheWritesFromNowOn();
        Task resuming = session.Continue();
        saves.LetTheWritesFinish();
        await resuming;
        await flush!;

        Assert.Equal(["load", "save", "set aside", "save"], saves.Operations);
        Assert.Equal(1, saves.MostOperationsInFlightAtOnce);
    }

    [Fact]
    public async Task ASaveThatCouldNotBeMovedAside_IsStillPlayedOverWithSavingHeldBack()
    {
        // #61's guarantee, unchanged by putting the move on the queue: when the two goals are in
        // direct conflict the one that cannot be undone still wins, and SaveError still says so.
        GatedSaveProgressService saves = new()
        {
            Content = "not a save this build can read",
            SetAsideFails = true,
        };
        GameSession session = CreateSession(saves);

        await session.Continue();

        Assert.True(session.IsReady);
        Assert.False(session.IsSavingProgress);
        Assert.IsType<IOException>(session.SaveError);
        Assert.Equal("not a save this build can read", saves.Content);
    }

    [Fact]
    public async Task AWriteQueuedAheadOfTheMove_DoesNotStopTheRefusedSaveBeingSetAside()
    {
        // The rule the queue already had for two writes, holding for a write in front of a move:
        // the failure of an operation belongs to whoever asked for it and does not take out the
        // one behind it.
        GatedSaveProgressService saves = new()
        {
            Content = "not a save this build can read",
            SaveFails = true,
        };
        GameSession session = CreateSession(saves);

        Task? flush = null;
        saves.WhenLoadBegins = () => flush = session.Save();

        await session.Continue();

        Assert.Contains("set aside", saves.Operations);
        Assert.True(session.IsReady);
        await Assert.ThrowsAsync<IOException>(() => flush!);
    }

    /// <summary>
    /// A save service that answers reads at once and holds writes until it is told to, and records
    /// every operation it ran in the order it finished — writes, reads and moves alike, because
    /// what is measured here is the order of all three against each other rather than of writes
    /// alone.
    /// </summary>
    sealed class GatedSaveProgressService : ISaveProgressService
    {
        TaskCompletionSource _released = Answered();

        int _inFlight;

        /// <summary>Gets or sets the content on disk.</summary>
        public string? Content { get; set; }

        /// <summary>Gets every operation that ran, in the order it finished.</summary>
        public List<string> Operations { get; } = [];

        /// <summary>Gets the greatest number of operations ever outstanding together.</summary>
        public int MostOperationsInFlightAtOnce { get; private set; }

        /// <summary>Gets or sets whether writes throw a storage failure.</summary>
        public bool SaveFails { get; set; }

        /// <summary>Gets or sets whether moving the save aside throws a storage failure.</summary>
        public bool SetAsideFails { get; set; }

        /// <summary>
        /// Gets or sets something to run as a read begins, before it is answered. The seam that
        /// makes "asked for while the read is in flight" a fixed sequence rather than a race —
        /// which is what a shutdown flush arriving on entry to a game actually is.
        /// </summary>
        public Action? WhenLoadBegins { get; set; }

        /// <summary>Answers everything waiting, and everything that comes after.</summary>
        public void LetTheWritesFinish() => _released.TrySetResult();

        /// <summary>Stops answering writes, so the next ones stay outstanding.</summary>
        public void HoldTheWritesFromNowOn() =>
            _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load()
        {
            Began();
            WhenLoadBegins?.Invoke();

            return Task.FromResult(Finished("load", () => Content));
        }

        public async Task Save(string content)
        {
            Began();

            await _released.Task;

            if (SaveFails)
            {
                Failed("failed save");
                throw new IOException("the file is in use.");
            }

            Finished("save", () => Content = content);
        }

        public async Task SetAside()
        {
            Began();

            await _released.Task;

            if (SetAsideFails)
            {
                Failed("failed set aside");
                throw new IOException("the file cannot be moved.");
            }

            Finished("set aside", () => Content = null);
        }

        static TaskCompletionSource Answered()
        {
            TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult();

            return source;
        }

        void Began()
        {
            _inFlight++;
            MostOperationsInFlightAtOnce = Math.Max(MostOperationsInFlightAtOnce, _inFlight);
        }

        void Failed(string operation)
        {
            Operations.Add(operation);
            _inFlight--;
        }

        T Finished<T>(string operation, Func<T> apply)
        {
            T result = apply();

            Operations.Add(operation);
            _inFlight--;

            return result;
        }
    }
}
