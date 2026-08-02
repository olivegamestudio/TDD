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
    public async Task Load_ReportsNoSave_WhenTheSaveIsGoneByTheTimeItIsRead()
    {
        // The case this pins is a save that answers "yes, I am here" and is then not there to
        // open — which is what SetAside makes of a save halfway through a Load, and what a
        // dangling symlink is at every moment. Staging it as a symlink is what makes it a check
        // rather than a coin toss: the real race reproduces about once in four hundred rounds,
        // which is a flaky test, not cover.
        Directory.CreateDirectory(_directory);
        if (!TryStageAFileThatIsNotThere())
        {
            // Some platforms only let a privileged process create one. The rule holds there too;
            // it just cannot be staged here.
            return;
        }

        Assert.True(File.Exists(SaveFile));

        Assert.Null(await CreateService().Load());
    }

    [Fact]
    public async Task Load_ReportsNoSave_WhenAnotherServiceSetTheSaveAsideFirst()
    {
        // The report's shape in the order that can be staged: two services sharing no gate, one
        // moving the save away and the other asked for it afterwards. The save really has gone,
        // so "there is no save" is the true answer and not a failure to report.
        LocalSaveProgressService reader = CreateService();
        await reader.Save("the game that was there a moment ago");

        await CreateService().SetAside();

        Assert.Null(await reader.Load());
    }

    [Fact]
    public async Task Load_DoesNotReportNoSave_WhenSomethingIsInTheWayOfReadingIt()
    {
        // "There is no save" is reserved for the save not being there. Anything else is storage
        // getting in the way, and the game holds saving back over it rather than starting a new
        // game on top of something that may well be intact.
        Directory.CreateDirectory(SaveFile);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService().Load());
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

    [Fact]
    public void DefaultsToAFileUnderTheUsersApplicationData()
    {
        LocalSaveProgressService service = new();

        Assert.EndsWith(Path.Combine("OliveGameStudio", "save.json"), service.FilePath);
    }

    /// <summary>
    /// Puts something at the save path that exists as far as an existence check is concerned and
    /// is not there to open — a symlink to a file that was never written.
    /// </summary>
    /// <returns><c>false</c> when the platform will not let this process create one.</returns>
    bool TryStageAFileThatIsNotThere()
    {
        try
        {
            File.CreateSymbolicLink(SaveFile, Path.Combine(_directory, "moved-away.json"));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
