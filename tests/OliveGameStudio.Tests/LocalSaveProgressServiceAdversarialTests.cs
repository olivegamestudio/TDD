namespace OliveGameStudio.Tests;

/// <summary>
/// QA's adversarial cover for the overlapping-write promise (#69). The shipped cover proves the
/// promise holds for two writers; these go after the places where the two mechanisms keeping it
/// stop overlapping — many writers rather than two, writers that share no service and are
/// therefore held apart by the atomic move alone, and a reader with no gate between it and any of
/// them.
/// </summary>
/// <remarks>
/// Every payload here is one character repeated, so a torn file is caught by its content rather
/// than only by its length: bytes from two writes cannot produce a run of a single character at
/// the right length by accident.
/// </remarks>
public sealed class LocalSaveProgressServiceAdversarialTests : IDisposable
{
    /// <summary>
    /// Large enough to tear. The issue is explicit that a test at today's ~300 byte save size
    /// passes without the fix and proves nothing.
    /// </summary>
    const int PayloadSize = 200_000;

    readonly string _directory = Path.Combine(Path.GetTempPath(), $"ogs-save-adv-{Guid.NewGuid():N}");

    string SaveFile => Path.Combine(_directory, "save.json");

    LocalSaveProgressService CreateService() => new(SaveFile);

    static string Payload(char content) => new(content, PayloadSize);

    /// <summary>
    /// Asserts the save holds the whole of exactly one payload — one character, repeated, at the
    /// full length. Anything else is a tear.
    /// </summary>
    static void AssertHoldsExactlyOnePayload(string saved)
    {
        Assert.Equal(PayloadSize, saved.Length);

        char first = saved[0];
        int mixedAt = saved.AsSpan().IndexOfAnyExcept(first);

        if (mixedAt >= 0)
        {
            Assert.Fail(
                $"the save holds bytes from more than one write: '{first}' up to {mixedAt}, "
                + $"then '{saved[mixedAt]}'");
        }
    }

    [Fact]
    public async Task ManyWritersAtOnce_LeaveTheWholeOfExactlyOneOfThem()
    {
        // Two writers is the case the fix was written against. Eight of them, twenty times over,
        // is where a gate that let anyone slip past would show.
        LocalSaveProgressService service = CreateService();

        for (int round = 0; round < 20; round++)
        {
            await Task.WhenAll(Enumerable
                .Range(0, 8)
                .Select(writer => service.Save(Payload((char)('a' + writer)))));

            AssertHoldsExactlyOnePayload(await File.ReadAllTextAsync(SaveFile));
        }
    }

    [Fact]
    public async Task ManyWritersThroughTheirOwnServices_StillLeaveExactlyOnePayload()
    {
        // No shared gate, so nothing but the write-elsewhere-and-move holds this together. This is
        // the shape of a shutdown flush racing an autosave, or a second copy of the game running.
        for (int round = 0; round < 20; round++)
        {
            await Task.WhenAll(Enumerable
                .Range(0, 8)
                .Select(writer => CreateService().Save(Payload((char)('a' + writer)))));

            AssertHoldsExactlyOnePayload(await File.ReadAllTextAsync(SaveFile));
        }
    }

    [Fact]
    public async Task AReaderRacingWritersItSharesNoGateWith_NeverSeesAPartialSave()
    {
        // The reader holds its own service, so the turn-taking cannot help it either. Criterion 5
        // has to survive that, because a half-read save is refused by the game and costs the
        // campaign exactly as a torn one does.
        await CreateService().Save(Payload('a'));

        using CancellationTokenSource writing = new();

        Task writers = Task.Run(async () =>
        {
            int writer = 0;
            while (!writing.IsCancellationRequested)
            {
                await CreateService().Save(Payload((char)('a' + (writer++ % 8))));
            }
        });

        for (int read = 0; read < 200; read++)
        {
            string? saved = await CreateService().Load();

            Assert.NotNull(saved);
            AssertHoldsExactlyOnePayload(saved);
        }

        await writing.CancelAsync();
        await writers;
    }

    [Fact]
    public async Task WritersThatOverlap_NeverFailOnAccountOfEachOther()
    {
        // Criterion 2. An overlap is not the storage getting in the way, so it must not arrive at
        // the player as a SaveError — which is what GameSession turns any IOException into.
        LocalSaveProgressService shared = CreateService();

        Exception? failure = await Record.ExceptionAsync(() => Task.WhenAll(Enumerable
            .Range(0, 16)
            .Select(writer => writer % 2 == 0
                ? shared.Save(Payload((char)('a' + writer)))
                : CreateService().Save(Payload((char)('a' + writer))))));

        Assert.Null(failure);
    }

    [Fact]
    public async Task HeavyConcurrency_LeavesNothingBehindButTheSave()
    {
        // A working file per write is what stops two services filling the same one. The cost is
        // that there are a lot of them, and a player may go looking through this folder.
        await Task.WhenAll(Enumerable
            .Range(0, 16)
            .Select(writer => CreateService().Save(Payload((char)('a' + writer)))));

        Assert.Equal(new[] { SaveFile }, Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task AnEmptySave_ReadsBackAsEmpty_NotAsNoSaveAtAll()
    {
        // Load answers null for "there is no save". An empty save is a different answer, and the
        // move-into-place path must not blur the two.
        LocalSaveProgressService service = CreateService();

        await service.Save(string.Empty);

        Assert.True(await service.HasProgress());
        Assert.Equal(string.Empty, await service.Load());
    }

    [Fact]
    public async Task ASaveFarLargerThanAnyBuffer_RoundTripsWhole()
    {
        LocalSaveProgressService service = CreateService();
        string content = new('x', 5_000_000);

        await service.Save(content);

        Assert.Equal(content, await service.Load());
    }

    [Fact]
    public async Task ContentThatIsNotPlainAscii_RoundTripsExactly()
    {
        // The save is written by one API and read by another; a mismatch in how they treat
        // encoding would be invisible until a player used a name the round trip mangled.
        LocalSaveProgressService service = CreateService();
        string content = "{\"name\":\"Ariadne Krähe\",\"note\":\"line\r\nbreak\tand 🛰\"}";

        await service.Save(content);

        Assert.Equal(content, await service.Load());
    }

    [Fact]
    public async Task ASaveThatFollowsASetAside_IsTheNewSaveAndNotTheOldOne()
    {
        // SetAside and Save both move a file into the same folder. Running them back to back must
        // leave the set-aside copy holding what was refused and the save holding what replaced it.
        LocalSaveProgressService service = CreateService();
        await service.Save(Payload('a'));

        await service.SetAside();
        await service.Save(Payload('b'));

        Assert.Equal(Payload('b'), await service.Load());
        Assert.Equal(Payload('a'), await File.ReadAllTextAsync(service.SetAsideFilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
