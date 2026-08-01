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
    public void DefaultsToAFileUnderTheUsersApplicationData()
    {
        LocalSaveProgressService service = new();

        Assert.EndsWith(Path.Combine("OliveGameStudio", "save.json"), service.FilePath);
    }

    [Fact]
    public async Task SetAside_KeepsTheContentSomewhereItCanStillBeRead()
    {
        // the whole point: a save a game refuses is still on the machine afterwards
        LocalSaveProgressService service = CreateService();
        await service.Save("the save the game would not resume from");

        await service.SetAside();

        Assert.Equal(
            "the save the game would not resume from",
            await File.ReadAllTextAsync(service.SetAsideFilePath));
    }

    [Fact]
    public async Task SetAside_LeavesNoSaveBehind()
    {
        // moved, not copied — otherwise the next launch reads the same unusable file again and
        // the game is stuck refusing it forever
        LocalSaveProgressService service = CreateService();
        await service.Save("{}");

        await service.SetAside();

        Assert.False(await service.HasProgress());
        Assert.Null(await service.Load());
    }

    [Fact]
    public async Task SetAside_KeepsTheSaveBesideTheOneItReplaced()
    {
        // found by whoever is asked for "your save file" without having to be told where to look
        LocalSaveProgressService service = CreateService();
        await service.Save("{}");

        await service.SetAside();

        Assert.Equal(service.FilePath + ".unreadable", service.SetAsideFilePath);
        Assert.Equal(Path.GetDirectoryName(service.FilePath), Path.GetDirectoryName(service.SetAsideFilePath));
    }

    [Fact]
    public async Task SetAside_DoesNothing_WhenThereIsNoSave()
    {
        // a first launch sets nothing aside, and must not fail trying
        LocalSaveProgressService service = CreateService();

        await service.SetAside();

        Assert.False(File.Exists(service.SetAsideFilePath));
    }

    [Fact]
    public async Task SetAside_KeepsOneGeneration_AndItIsTheMostRecent()
    {
        // The stated cost of keeping one: a second refusal displaces the first file kept. Pinned
        // as a test rather than left in a comment, because it is the part of this that loses
        // something, and a change of mind about it should have to change a test that says so.
        LocalSaveProgressService service = CreateService();
        await service.Save("the first refusal");
        await service.SetAside();
        await service.Save("the second refusal");

        await service.SetAside();

        Assert.Equal("the second refusal", await File.ReadAllTextAsync(service.SetAsideFilePath));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task SetAside_ThenSavingAgain_LeavesBothFiles()
    {
        // the new game writes a save of its own, and doing so must not disturb what was kept
        LocalSaveProgressService service = CreateService();
        await service.Save("the refused save");
        await service.SetAside();

        await service.Save("the new game");

        Assert.Equal("the new game", await service.Load());
        Assert.Equal("the refused save", await File.ReadAllTextAsync(service.SetAsideFilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
