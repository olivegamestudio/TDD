using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial pass over #46, driving <see cref="GameSession.Continue"/> against a
/// <see cref="LocalSaveProgressService"/> on a real directory rather than an in-memory stand-in.
/// </summary>
/// <remarks>
/// <para>
/// The change is covered from both ends and joined in neither: the session tests substitute
/// <see cref="InMemorySaveProgressService"/>, whose <c>SetAside</c> is three lines that cannot fail
/// and cannot be mis-ordered, and the storage tests drive <c>SetAside</c> directly without a
/// session anywhere near it. The claim the issue actually makes — that the refused bytes are still
/// on disk after the new game has written over the save — is a claim about the two together.
/// </para>
/// <para>
/// It is also the ordering that carries the whole fix. Set aside after the new game saves instead
/// of before, and every test here still passes against the fake, because the fake's Content is
/// whatever was written last either way. On a real file system it is the difference between
/// keeping the player's game and keeping a copy of the one that replaced it.
/// </para>
/// </remarks>
public sealed class RefusedSaveEndToEndAdversarialTests : IDisposable
{
    static readonly Position Start = new(0, 0);
    static readonly Position End = new(0, 1000);

    readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qa-refused-save-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    sealed class TestCampaign(params QuestDefinition[] quests) : ICampaign
    {
        public IReadOnlyList<QuestDefinition> Quests { get; } = quests;
    }

    sealed class TestWorld(params QuestMarkers[] markers) : IWorld
    {
        public Position PlayerStart => Start;

        // a test world that names no places: whatever enters it enters where it starts
        public Position Introduce(Ship ship, string location) => PlayerStart;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = markers;
    }

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true);

    LocalSaveProgressService CreateStore(string fileName = "save.json") =>
        new(Path.Combine(_directory, fileName));

    static GameSession CreateSession(ISaveProgressService store) =>
        new(store,
            new TestCampaign(Quest("quest-1")),
            new TestRoster(),
            new TestWorld(new QuestMarkers("quest-1", Start, End)));

    /// <summary>
    /// The save from the issue: real progress 700 units in, in a shape this build will not take.
    /// Deliberately not gibberish — a quest state of <c>99</c> is exactly the sort of thing a later
    /// build might read perfectly well, which is the entire argument for keeping the file.
    /// </summary>
    const string RefusedSave = """
        {
          "PlayerX": 0,
          "PlayerY": 700,
          "Quests": [ { "QuestId": "quest-1", "State": 99 } ]
        }
        """;

    static async Task GivenSaveFile(LocalSaveProgressService store, string content)
    {
        await store.Save(content);

        // the save has to be one this build genuinely refuses, or the test proves nothing
        Assert.Null(SaveGameSerializer.Deserialize(content));
    }

    // ---- the acceptance criterion, end to end on a real file system ----

    [Fact]
    public async Task ARefusedSave_IsStillOnDisk_AfterTheNewGameHasWrittenOverIt()
    {
        // QA's original ARefusedSave_IsOverwrittenByTheNewGame, inverted: the same 700-unit save,
        // the same Continue(), and now the bytes survive it. Byte-for-byte rather than
        // "deserialises to something similar" — a later build has to get what was actually there.
        LocalSaveProgressService store = CreateStore();
        await GivenSaveFile(store, RefusedSave);

        GameSession session = CreateSession(store);
        await session.Continue();

        Assert.Equal(RefusedSave, await File.ReadAllTextAsync(store.SetAsideFilePath));

        // and the player got a playable new game, saved over the space the old one vacated
        Assert.True(session.IsReady);
        Assert.True(session.IsSavingProgress);
        Assert.Equal(Start, session.Player.Position);

        SaveGame? written = SaveGameSerializer.Deserialize(await store.Load());
        Assert.NotNull(written);
        Assert.Equal(0, written.PlayerY, 6);
    }

    [Fact]
    public async Task TheNewGamesSave_AndTheRefusedOne_AreTwoDifferentFiles()
    {
        // the ordering the whole fix rests on. Set aside after the new game writes rather than
        // before and this is the test that notices: the kept file would hold the fresh game.
        LocalSaveProgressService store = CreateStore();
        await GivenSaveFile(store, RefusedSave);

        await CreateSession(store).Continue();

        Assert.NotEqual(store.FilePath, store.SetAsideFilePath);
        Assert.True(File.Exists(store.FilePath));
        Assert.True(File.Exists(store.SetAsideFilePath));
        Assert.NotEqual(
            await File.ReadAllTextAsync(store.FilePath),
            await File.ReadAllTextAsync(store.SetAsideFilePath));
    }

    // ---- orderings and repeats ----

    [Fact]
    public async Task ASecondContinue_OverTheGoodSaveTheFirstOneWrote_DoesNotDisturbTheKeptFile()
    {
        // the kept file must not be quietly replaced by a run where nothing was refused. Continue
        // again and the save now reads fine, so there is no second refusal and nothing to set
        // aside — but a SetAside called unconditionally would overwrite the rescue with a healthy
        // save and lose the player's game on the second press of the button rather than the first.
        LocalSaveProgressService store = CreateStore();
        await GivenSaveFile(store, RefusedSave);

        GameSession first = CreateSession(store);
        await first.Continue();

        GameSession second = CreateSession(store);
        await second.Continue();

        Assert.Equal(RefusedSave, await File.ReadAllTextAsync(store.SetAsideFilePath));
        Assert.True(second.IsReady);
    }

    [Fact]
    public async Task ASecondRefusal_ReplacesTheKeptFile_AndKeepsTheMoreRecentGame()
    {
        // one generation, and it is the most recent — the documented decision. Pinned end to end
        // so that changing it has to change a test that says so.
        LocalSaveProgressService store = CreateStore();
        await GivenSaveFile(store, RefusedSave);
        await CreateSession(store).Continue();

        const string laterRefusal = """{ "PlayerX": 1, "PlayerY": 2, "Quests": [ { "QuestId": "q", "State": 99 } ] }""";
        await GivenSaveFile(store, laterRefusal);
        await CreateSession(store).Continue();

        Assert.Equal(laterRefusal, await File.ReadAllTextAsync(store.SetAsideFilePath));
    }

    // ---- the file naming, including the collision the handoff flagged and did not guard ----

    [Fact]
    public async Task ASaveAlreadyNamedLikeASetAsideFile_DoesNotSetItselfAside()
    {
        // the handoff called this out as an unguarded collision. It is worth proving rather than
        // assuming either way: if SetAsideFilePath ever equalled FilePath, the move would be a
        // file onto itself and the refused save would be gone for good — the exact loss this
        // issue exists to stop.
        LocalSaveProgressService store = CreateStore("save.corrupt.json");
        await GivenSaveFile(store, RefusedSave);

        Assert.NotEqual(store.FilePath, store.SetAsideFilePath);

        await CreateSession(store).Continue();

        Assert.Equal(RefusedSave, await File.ReadAllTextAsync(store.SetAsideFilePath));
    }

    [Fact]
    public async Task ASaveWithNoExtension_IsStillSetAsideSomewhereRecoverable()
    {
        // the name is built by splitting on the extension, so a save file without one is the case
        // that would produce an empty or duplicated name if the split were done carelessly
        LocalSaveProgressService store = CreateStore("save");
        await GivenSaveFile(store, RefusedSave);

        await CreateSession(store).Continue();

        Assert.Equal(Path.Combine(_directory, "save.corrupt"), store.SetAsideFilePath);
        Assert.Equal(RefusedSave, await File.ReadAllTextAsync(store.SetAsideFilePath));
    }

    // ---- a blank file is refused too, and is deliberately not kept ----

    [Fact]
    public async Task ABlankSaveIsNotKept_BecauseThereIsNothingInItToRecover()
    {
        // Deserialize refuses whitespace exactly as it refuses damaged JSON, so on a literal
        // reading of criterion 1 this file is "a save the serialiser refused" and is not kept.
        // That is deliberate: there is nothing in it a later build could read, and leaving a
        // .corrupt file would tell the player something was lost when nothing was. Pinned so the
        // exception to the rule is a decision on the record rather than a gap.
        LocalSaveProgressService store = CreateStore();
        await GivenSaveFile(store, "   \n\t  ");

        GameSession session = CreateSession(store);
        await session.Continue();

        Assert.False(File.Exists(store.SetAsideFilePath));
        Assert.True(session.IsReady);
    }

    // ---- when the rescue works and the replacement does not ----

    [Fact]
    public async Task ARefusedSaveIsKept_EvenWhenTheNewGameCannotBeWritten()
    {
        // the rescue and the replacement are two writes and either can fail on its own. If the
        // new game's save throws after the move has already happened, the player must still be
        // left with a playable game AND the kept file — losing the rescue because the thing that
        // replaced it failed would be the worst of both.
        FailingSaveProgressService store = new(saveError: new IOException("disk full"))
        {
            Content = RefusedSave,
        };

        GameSession session = CreateSession(store);
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.IsType<IOException>(session.SaveError);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task ARefusedSaveThatCannotBeMoved_LeavesThePlayerAGame_AndTheFileIntact()
    {
        // the conflict case: preserving and replacing cannot both happen, so nothing is written.
        // The file the player would have lost is still exactly where it was.
        FailingSaveProgressService store = new(setAsideError: new UnauthorizedAccessException("read only"))
        {
            Content = RefusedSave,
        };

        GameSession session = CreateSession(store);
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.False(session.IsSavingProgress);
        Assert.Equal(RefusedSave, store.Content);
        Assert.Equal(0, store.SaveCount);
        Assert.IsType<UnauthorizedAccessException>(session.SaveError);
    }
}
