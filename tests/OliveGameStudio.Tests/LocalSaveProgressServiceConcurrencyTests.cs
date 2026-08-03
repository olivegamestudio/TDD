namespace OliveGameStudio.Tests;

/// <summary>
/// What the save service does when more than one operation reaches the same file at once. These
/// are separate from <see cref="LocalSaveProgressServiceTests"/> because they are about the
/// contract rather than about saving and loading: nothing here cares what the content is, only
/// that whoever reads the file gets the whole of something somebody wrote.
/// </summary>
public sealed class LocalSaveProgressServiceConcurrencyTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogs-save-concurrency-{Guid.NewGuid():N}");

    string SaveFile => Path.Combine(_directory, "save.json");

    LocalSaveProgressService CreateService() => new(SaveFile);

    /// <summary>
    /// A payload big enough that writing it is not a single syscall, so a write can be caught
    /// halfway. At the game's real save size — a few hundred bytes — an overlapping write lands
    /// whole on Linux by luck of the buffer size, so a test at that size passes against the defect
    /// and proves nothing.
    /// </summary>
    static string Payload(char letter) => new(letter, 200_000);

    /// <summary>
    /// The payload used where two <em>writers</em> have to collide, which is much harder to catch
    /// than a reader catching a writer. <see cref="LocalSaveProgressService.Save"/> creates the save
    /// folder before it writes, and that syscall is enough of a head start that the 200 KB pair QA
    /// measured tore only twice in two hundred — too rare to be a check. At this size it tore 16
    /// times in 20.
    /// </summary>
    static string LargePayload(char letter) => new(letter, 10_000_000);

    /// <summary>
    /// How many times a case that depends on two operations actually overlapping is run. One round
    /// is a coin toss — the defect these pin does not fail every attempt — and the point of a
    /// regression test is that it fails every time the defect is back, not most times.
    /// </summary>
    const int Rounds = 8;

    /// <summary>
    /// Starts work on the thread pool rather than awaiting it here. xUnit runs a test on a
    /// synchronisation context that resumes continuations one at a time, so two writes awaited
    /// directly take turns and the overlap these tests exist to create never happens.
    /// </summary>
    static Task Concurrently(Func<Task> work) => Task.Run(work);

    [Fact]
    public async Task TwoWritesAtOnce_LeaveTheWholeOfOneOfThem_AndNeverBytesFromBoth()
    {
        // File.WriteAllTextAsync truncates and rewrites with no serialisation between callers, so
        // two writers of a payload this size interleave and the file ends up holding the front of
        // one and the back of the other. A torn save is not a corrupt file the player can be
        // warned about — the game refuses what it cannot parse and starts a new game over it — so
        // what tearing costs is the campaign, not the file.
        LocalSaveProgressService service = CreateService();
        string first = LargePayload('a');
        string second = LargePayload('b');

        for (int round = 0; round < Rounds; round++)
        {
            await Task.WhenAll(
                Concurrently(() => service.Save(first)),
                Concurrently(() => service.Save(second)));

            AssertHolds(await File.ReadAllTextAsync(SaveFile), first, second);
        }
    }

    [Fact]
    public async Task TwoWritesAtOnce_ThroughSeparateServices_LeaveTheWholeOfOneOfThem()
    {
        // A gate inside one instance cannot see another instance, so serialising alone is not the
        // whole answer: the write itself has to be all or nothing. This is the case a shutdown
        // flush, a second process, or another game built on the engine reaches.
        string first = LargePayload('a');
        string second = LargePayload('b');

        for (int round = 0; round < Rounds; round++)
        {
            await Task.WhenAll(
                Concurrently(() => CreateService().Save(first)),
                Concurrently(() => CreateService().Save(second)));

            AssertHolds(await File.ReadAllTextAsync(SaveFile), first, second);
        }
    }

    [Fact]
    public async Task AWriteDoesNotFail_BecauseAnotherWasInProgress()
    {
        // An overlap is not storage trouble and must not be reported as any. On Windows the second
        // File.WriteAllTextAsync fails FileShare and throws IOException, which GameSession.TrySave
        // catches and shows the player as a save error — for a write that was only unlucky in its
        // timing. Nothing is asserted beyond the absence of a throw, which is the whole claim.
        LocalSaveProgressService service = CreateService();

        for (int round = 0; round < Rounds; round++)
        {
            await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(index => Concurrently(() => service.Save(Payload((char)('a' + index))))));
        }
    }

    [Fact]
    public async Task AWriteDoesNotFail_BecauseAnotherServiceWasWritingTheSameFile()
    {
        for (int round = 0; round < Rounds; round++)
        {
            await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(index => Concurrently(() => CreateService().Save(Payload((char)('a' + index))))));
        }
    }

    [Fact]
    public async Task AReadOverlappingAWrite_SeesOneWholeSaveOrTheOther_NeverPartOfOne()
    {
        // The reader's half of the same rule, and the one a gate cannot give: the save is replaced
        // by moving a finished file over the old one, so the path names a whole save at every
        // moment and there is no window in which it names half of one.
        string before = Payload('a');
        string after = Payload('b');

        for (int round = 0; round < Rounds; round++)
        {
            await CreateService().Save(before);

            Task write = Concurrently(() => CreateService().Save(after));
            Task<string?>[] reads =
            [
                .. Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateService().Load())),
            ];
            await Task.WhenAll([write, .. reads]);

            Assert.All(reads, read => AssertHolds(read.Result, before, after));
        }
    }

    [Fact]
    public async Task ARefusedSaveIsStillSetAsideWhole_WhenAWriteIsRunningBesideIt()
    {
        // Set-aside and save go through the same gate within a service, so the file kept for
        // recovery is one somebody actually wrote rather than one caught halfway. Refusing a save
        // is this build's judgement and a later build may read it perfectly well — but only if what
        // is kept is whole.
        string before = Payload('a');
        string after = Payload('b');

        for (int round = 0; round < Rounds; round++)
        {
            LocalSaveProgressService service = CreateService();
            await service.Save(before);

            await Task.WhenAll(
                Concurrently(() => service.Save(after)),
                Concurrently(() => service.SetAside()));

            AssertHolds(await File.ReadAllTextAsync(service.SetAsideFilePath), before, after);
        }
    }

    // ---- a save that goes while it is being read ----

    [Fact]
    public async Task Load_AnswersNoSave_WhenTheSaveHasGone()
    {
        // SetAside moves the save away, so it is the one operation that can make the path stop
        // existing while somebody is reading it. A file that is not there when the read opens it is
        // the true answer "there is no save" rather than a failure — a read that was only unlucky
        // in its timing must not reach the player as storage trouble, which is what
        // GameSession.TrySave turns an IOException into.
        LocalSaveProgressService service = CreateService();
        await service.Save("a game");
        File.Delete(SaveFile);

        Assert.Null(await service.Load());
    }

    [Fact]
    public async Task Load_AnswersNoSave_WhenTheSaveFolderIsNotThere()
    {
        // The same question one level up: a folder removed underneath the read raises
        // DirectoryNotFoundException rather than FileNotFoundException, and it means the same
        // thing.
        LocalSaveProgressService service = CreateService();
        await service.Save("a game");
        Directory.Delete(_directory, recursive: true);

        Assert.Null(await service.Load());
    }

    [Fact]
    public async Task Load_RacingASetAside_AnswersRatherThanFailing()
    {
        // The race itself, through services that share no gate. Load used to ask whether the file
        // existed and then open it, and a set-aside landing between the two questions left it
        // opening a file that had gone.
        //
        // This is cover, not the check — it reproduces about once in four hundred rounds against
        // the defect, so it is the two tests above that state what Load now answers. It is kept
        // because it is the only thing here that exercises the window itself, and after the fix
        // there is no window: Load asks the file system once, so it cannot fail on a round it
        // happens to lose.
        for (int round = 0; round < 400; round++)
        {
            await CreateService().Save(Payload('a'));

            Task<string?> read = Task.Run(() => CreateService().Load());
            Task aside = Task.Run(() => CreateService().SetAside());

            await Task.WhenAll(read, aside);
        }
    }

    /// <summary>
    /// Asserts that what was read is the whole of one of the two payloads written, reporting what
    /// it actually held rather than dumping 200,000 characters into the failure.
    /// </summary>
    static void AssertHolds(string? content, string first, string second)
    {
        if (content == first || content == second)
        {
            return;
        }

        Assert.Fail(
            content is null
                ? "nothing was read where a whole save was written."
                : $"neither payload was there whole: {content.Length} characters, "
                    + $"[{string.Join(", ", content.Distinct())}] in it.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
