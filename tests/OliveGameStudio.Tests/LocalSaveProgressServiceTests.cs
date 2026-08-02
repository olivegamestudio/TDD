namespace OliveGameStudio.Tests;

public sealed class LocalSaveProgressServiceTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogs-save-tests-{Guid.NewGuid():N}");

    string SaveFile => Path.Combine(_directory, "save.json");

    LocalSaveProgressService CreateService() => new(SaveFile);

    [Fact]
    public async Task HasProgress_IsFalse_BeforeAnythingIsSaved()
    {
        LocalSaveProgressService service = CreateService();

        Assert.False(await service.HasProgress());
    }

    [Fact]
    public async Task HasProgress_IsTrue_OnceSomethingIsSaved()
    {
        LocalSaveProgressService service = CreateService();

        await service.Save("{}");

        Assert.True(await service.HasProgress());
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenThereIsNoSave()
    {
        LocalSaveProgressService service = CreateService();

        Assert.Null(await service.Load());
    }

    [Fact]
    public async Task RoundTripsTheSavedContent()
    {
        LocalSaveProgressService service = CreateService();

        await service.Save("""{"PlayerX":1.5}""");

        Assert.Equal("""{"PlayerX":1.5}""", await service.Load());
    }

    [Fact]
    public async Task Save_CreatesTheDirectory_OnFirstUse()
    {
        // a fresh install has no save folder yet
        Assert.False(Directory.Exists(_directory));
        LocalSaveProgressService service = CreateService();

        await service.Save("{}");

        Assert.True(File.Exists(SaveFile));
    }

    [Fact]
    public async Task Save_ReplacesThePreviousSave()
    {
        LocalSaveProgressService service = CreateService();
        await service.Save("first");

        await service.Save("second");

        Assert.Equal("second", await service.Load());
    }

    [Fact]
    public async Task ASecondInstanceReadsWhatTheFirstWrote()
    {
        // the save must survive the process, not just the instance
        await CreateService().Save("persisted");

        Assert.Equal("persisted", await CreateService().Load());
    }

    [Fact]
    public async Task SetAside_MovesTheSaveOutOfTheWay_LeavingNoneBehind()
    {
        // the point of it: after this the game may write a new save without destroying the old one
        LocalSaveProgressService service = CreateService();
        await service.Save("the game that could not be read");

        await service.SetAside();

        Assert.False(await service.HasProgress());
        Assert.Null(await service.Load());
        Assert.Equal("the game that could not be read", await File.ReadAllTextAsync(service.SetAsideFilePath));
    }

    [Fact]
    public void SetAside_NamesTheFileBesideTheSave_KeepingItsExtension()
    {
        // support has to be able to ask for it by name, and the player has to be able to see what
        // it is; save.json becomes save.corrupt.json rather than something with no extension
        LocalSaveProgressService service = CreateService();

        Assert.Equal(Path.Combine(_directory, "save.corrupt.json"), service.SetAsideFilePath);
    }

    [Fact]
    public async Task SetAside_DoesNothing_WhenThereIsNoSave()
    {
        // starting a genuinely new game must not leave an empty file behind pretending to be a
        // recoverable one
        LocalSaveProgressService service = CreateService();

        await service.SetAside();

        Assert.False(File.Exists(service.SetAsideFilePath));
    }

    [Fact]
    public async Task SetAside_KeepsTheLatestGeneration_WhenItHappensTwice()
    {
        // one generation, deliberately: an unbounded pile of them is its own kind of mess, and the
        // most recent refusal is the one a later build would be asked to read
        LocalSaveProgressService service = CreateService();
        await service.Save("first refusal");
        await service.SetAside();
        await service.Save("second refusal");

        await service.SetAside();

        Assert.Equal("second refusal", await File.ReadAllTextAsync(service.SetAsideFilePath));
    }

    [Fact]
    public async Task SetAside_LeavesTheSetAsideFileReadableByAnotherInstance()
    {
        // it survives the process, or it is not recovery
        LocalSaveProgressService first = CreateService();
        await first.Save("persisted");
        await first.SetAside();

        Assert.Equal("persisted", await File.ReadAllTextAsync(CreateService().SetAsideFilePath));
    }

    // ---- two writes at once ----

    /// <summary>
    /// Big enough that a write cannot land in one syscall. At the game's real save size — a few
    /// hundred bytes — two overlapping writes do not tear on Linux whatever the implementation
    /// does, so a test at that size would pass without the file ever being safe and prove nothing.
    /// </summary>
    const int PayloadLargeEnoughToTear = 200_000;

    static readonly string Payload1 = new('1', PayloadLargeEnoughToTear);

    static readonly string Payload2 = new('2', PayloadLargeEnoughToTear);

    /// <summary>
    /// How many overlapping pairs each concurrency test runs. Tearing is a race, so one pair
    /// proves nothing either way; measured against an unguarded write, a couple of hundred pairs
    /// is where it shows up.
    /// </summary>
    const int Pairs = 200;

    [Fact]
    public async Task ConcurrentWrites_NeverLeaveAFileHoldingBytesFromBoth()
    {
        // A torn save is not a corrupt file the player can be warned about — the game refuses what
        // it cannot read and replaces it, so tearing costs a campaign outright.
        LocalSaveProgressService service = CreateService();

        for (int pair = 0; pair < Pairs; pair++)
        {
            await Task.WhenAll(service.Save(Payload1), service.Save(Payload2));

            string onDisk = await File.ReadAllTextAsync(SaveFile);
            Assert.True(
                onDisk == Payload1 || onDisk == Payload2,
                $"pair {pair} left a file of {onDisk.Length} characters holding bytes from both writes");
        }
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotFailBecauseTheOtherWasInProgress()
    {
        // An overlap is not storage getting in the way, and must not be reported as it. On Windows
        // the second unguarded write fails FileShare and throws, which the game turns into a
        // SaveError on a game that was saving perfectly well.
        LocalSaveProgressService service = CreateService();

        for (int pair = 0; pair < Pairs; pair++)
        {
            // the assertion is that this does not throw
            await Task.WhenAll(service.Save(Payload1), service.Save(Payload2));
        }

        Assert.True(await service.HasProgress());
    }

    [Fact]
    public async Task ConcurrentWritesThroughTwoServices_StillNeverTearTheFile()
    {
        // Two services on one file stand in for two processes — a second copy of the game, or a
        // shutdown flush racing an autosave. Nothing in-process can order those, so this is the
        // half of the answer that has to hold without any gate: whichever write wins, it lands
        // whole or not at all.
        LocalSaveProgressService first = CreateService();
        LocalSaveProgressService second = CreateService();

        for (int pair = 0; pair < Pairs; pair++)
        {
            await Task.WhenAll(first.Save(Payload1), second.Save(Payload2));

            string onDisk = await File.ReadAllTextAsync(SaveFile);
            Assert.True(
                onDisk == Payload1 || onDisk == Payload2,
                $"pair {pair} left a file of {onDisk.Length} characters holding bytes from both writes");
        }
    }

    [Fact]
    public async Task AReaderRacingAWriter_NeverSeesAHalfWrittenSave()
    {
        // The reader is the game deciding whether to offer Continue. A partial read is a save that
        // will not parse, which is indistinguishable from a damaged one and costs the same game.
        LocalSaveProgressService service = CreateService();
        await service.Save(Payload1);

        List<string> readBack = [];
        Task reading = Task.Run(async () =>
        {
            for (int read = 0; read < Pairs; read++)
            {
                readBack.Add(await service.Load() ?? string.Empty);
            }
        });

        for (int write = 0; write < Pairs; write++)
        {
            await service.Save(write % 2 == 0 ? Payload1 : Payload2);
        }

        await reading;

        Assert.All(readBack, content => Assert.True(
            content == Payload1 || content == Payload2,
            $"a read saw {content.Length} characters, which is neither whole save"));
    }

    [Fact]
    public async Task Save_LeavesNothingBesideTheSave_OnceItHasFinished()
    {
        // a save written somewhere else first and moved into place must not leave the somewhere
        // else behind, or the save folder fills up with files the player cannot account for
        LocalSaveProgressService service = CreateService();

        await Task.WhenAll(service.Save(Payload1), service.Save(Payload2));

        Assert.Equal([SaveFile], Directory.GetFiles(_directory));
    }

    [Fact]
    public void DefaultsToAFileUnderTheUsersApplicationData()
    {
        LocalSaveProgressService service = new();

        Assert.EndsWith(Path.Combine("OliveGameStudio", "save.json"), service.FilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
