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

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
