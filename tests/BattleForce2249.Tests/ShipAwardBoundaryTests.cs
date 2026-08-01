using System.Globalization;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// A second adversarial pass over the ship award, going after the edges
/// <see cref="ShipAwardAdversarialTests"/> leaves alone: values that are not strings at all, a save
/// written by a machine set to another language, the read that fails rather than the one that is
/// damaged, and a shipping yard that hands the session nothing.
/// </summary>
/// <remarks>
/// <para>
/// The previous pass established that a <em>string</em> identifier the build cannot resolve leaves
/// the player downgraded rather than grounded. The cases here are the ones where the save is not
/// merely naming the wrong ship but is the wrong shape, or where the failure is in the storage
/// rather than in the file — because those reach different code, and "the save is forgiving" is
/// only worth anything if it holds for the shapes nobody thought to write.
/// </para>
/// <para>
/// Culture is pinned around every identifier assertion, and around the numbers as well: a save
/// written on a machine set to German has to load on one set to English, so the decimal separator
/// matters as much as the dotted I does.
/// </para>
/// </remarks>
public sealed class ShipAwardBoundaryTests
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

    /// <summary>
    /// A yard with nothing in it, standing in for a game whose content failed to load. The session
    /// has to fail loudly here rather than hand a null ship on to whatever draws it.
    /// </summary>
    sealed class EmptyShipYard : IShipYard
    {
        public IReadOnlyList<Ship> Ships { get; } = [];

        public Ship StartingShip => null!;

        public Ship? Find(string shipId) => null;
    }

    /// <summary>
    /// Runs part of a test as though the machine were set to another language, formatting included.
    /// <see cref="CultureScope"/> moves the UI culture only, which is the right scope for text the
    /// player reads and the wrong one for asking whether a number was written with a comma.
    /// </summary>
    sealed class FormattingScope : IDisposable
    {
        readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public FormattingScope(string culture) =>
            CultureInfo.CurrentCulture = new CultureInfo(culture);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    static QuestDefinition Quest(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession(ISaveProgressService? saves = null, IShipYard? ships = null) =>
        new(saves ?? _saves,
            new TestCampaign(Quest("quest-1")),
            new TestWorld(new QuestMarkers("quest-1", Start, End)),
            ships ?? new TestShipYard());

    // ---- the identifier that is not a string ----

    /// <summary>
    /// Every earlier case feeds the resume path a string it cannot resolve. These feed it something
    /// that is not a string at all, which is a different failure: the deserialiser throws rather
    /// than handing back a save with an odd value in it, so the fallback that catches it is
    /// <see cref="SaveGameSerializer"/>'s <c>catch</c> rather than the yard's miss.
    /// </summary>
    [Theory]
    [InlineData("42")]                  // a number, from a build that keyed ships by index
    [InlineData("[]")]                   // an array
    [InlineData("[\"disgraced\"]")]      // an array with the right answer in it
    [InlineData("{}")]                   // an object
    [InlineData("{\"Id\":\"earned\"}")]  // the whole ship, from a build that wrote it by value
    [InlineData("true")]                 // a boolean
    public async Task AShipIdThatIsNotAString_StartsANewGame_RatherThanThrowing(string json)
    {
        _saves.Content = $$"""
            { "PlayerX": 5, "PlayerY": 7, "ShipId": {{json}}, "Quests": [] }
            """;

        GameSession session = CreateSession();
        await session.Continue();

        // the save could not be read at all, so this is a new game: the starting ship, and the
        // world's start rather than the position the unreadable file named
        Assert.True(session.IsReady);
        Assert.Same(TestShipYard.Starter, session.Player.Ship);
        Assert.Equal(Start, session.Player.Position);
    }

    /// <summary>
    /// A hand edit that leaves the old line in place. Whichever of the two wins, the session must
    /// settle on one of them and stay readable — the failure to rule out is a throw on load.
    /// </summary>
    [Fact]
    public async Task AShipIdNamedTwice_ResolvesToTheLastOne()
    {
        _saves.Content = """
            { "PlayerX": 0, "PlayerY": 0, "ShipId": "starter", "ShipId": "earned", "Quests": [] }
            """;

        GameSession session = CreateSession();
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Same(TestShipYard.Earned, session.Player.Ship);
    }

    /// <summary>
    /// An identifier long enough to have come from something other than a ship. It must miss and
    /// fall back like any other miss, without the exact match being asked to do anything clever.
    /// </summary>
    [Fact]
    public async Task AnAbsurdlyLongShipId_MissesAndFallsBack()
    {
        string huge = new('a', 100_000);
        _saves.Content = $$"""
            { "PlayerX": 5, "PlayerY": 7, "ShipId": "{{huge}}", "Quests": [] }
            """;

        GameSession session = CreateSession();
        await session.Continue();

        Assert.Same(TestShipYard.Starter, session.Player.Ship);

        // the rest of the save is still honoured — an unresolvable ship costs the ship and nothing
        // else, which is the whole point of the fallback
        Assert.Equal(new Position(5, 7), session.Player.Position);
    }

    /// <summary>
    /// The asset key is not the identifier. They are both strings on the same record and a save
    /// written by a confused build could carry one where the other belongs, so the yard must not
    /// resolve it — awarding a ship because its <em>artwork</em> was named would put a hull in the
    /// cockpit that nothing awarded.
    /// </summary>
    [Fact]
    public void TheAssetKeyIsNotAnIdentifier_AndDoesNotResolve()
    {
        BattleForceShipYard yard = new();

        Assert.Null(yard.Find(BattleForceShip.AssetKey));
        Assert.NotNull(yard.Find(BattleForceShip.Id));
    }

    // ---- the machine set to another language ----

    /// <summary>
    /// A save written on a machine set to German and loaded on one set to English. The identifier
    /// is the case the earlier pass covered; the numbers are the case it did not, and a decimal
    /// separator written as a comma would strand the player somewhere they never flew to — or
    /// refuse the save outright.
    /// </summary>
    [Fact]
    public async Task ASaveWrittenUnderGerman_LoadsUnderEnglish_PositionAndShipAlike()
    {
        using (new FormattingScope("de-DE"))
        {
            GameSession writing = CreateSession();
            await writing.Continue();
            writing.Player.MoveTo(new Position(1234.5678, -987.6543));
            writing.Player.Award(TestShipYard.Earned);
            await writing.Save();
        }

        using (new FormattingScope("en-GB"))
        {
            GameSession reading = CreateSession();
            await reading.Continue();

            Assert.Same(TestShipYard.Earned, reading.Player.Ship);
            Assert.Equal(1234.5678, reading.Player.Position.X);
            Assert.Equal(-987.6543, reading.Player.Position.Y);
        }
    }

    /// <summary>
    /// Matching is ordinal, and ordinal has to mean ordinal on a Turkish machine too. This sets the
    /// formatting culture rather than the UI culture, because a careless <c>ToLower()</c> in the
    /// yard would read the former: under <c>tr-TR</c>, lowercasing <c>DISGRACED</c> does not
    /// produce <c>disgraced</c>, and lowercasing <c>disgraced</c> does not produce what
    /// uppercasing it round-trips to.
    /// </summary>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void TheIdentifierIsMatchedOrdinally_EvenWhereTheAlphabetDisagrees(string culture)
    {
        using FormattingScope _ = new(culture);
        BattleForceShipYard yard = new();

        Assert.NotNull(yard.Find("disgraced"));
        Assert.Null(yard.Find("DISGRACED"));
        Assert.Null(yard.Find("DİSGRACED"));
        Assert.Null(yard.Find("dısgraced"));
    }

    // ---- the read that fails rather than the file that is damaged ----

    /// <summary>
    /// A save that could not be <em>read</em> is not a save that is wrong. The session gives the
    /// player something to fly, and must not write over the file it failed to read — a save locked
    /// for a moment by cloud sync is not a save the player lost, and overwriting it would make it
    /// one.
    /// </summary>
    [Fact]
    public async Task AReadThatFails_StillLeavesThePlayerInAShip_AndDoesNotOverwriteTheSave()
    {
        const string held = """
            { "PlayerX": 11, "PlayerY": 22, "ShipId": "earned", "Quests": [] }
            """;
        FailingSaveProgressService storage =
            new(loadError: new IOException("the save is locked")) { Content = held };

        GameSession session = CreateSession(storage);
        await session.Continue();

        // playable
        Assert.True(session.IsReady);
        Assert.NotNull(session.Player.Ship);
        Assert.Same(TestShipYard.Starter, session.Player.Ship);

        // and the earned ship is still on disk, unread but not lost
        Assert.Equal(0, storage.SaveCount);
        Assert.Equal(held, storage.Content);
        Assert.False(session.IsSavingProgress);
        Assert.NotNull(session.SaveError);
    }

    /// <summary>
    /// After a failed read the player may still choose to start over, and that choice is theirs to
    /// make — so the new game saves normally, taking the earned ship back with it. The held save is
    /// protected from being overwritten <em>by accident</em>, not from being overwritten at all.
    /// </summary>
    [Fact]
    public async Task StartingANewGameAfterAFailedRead_SavesAgain_WithTheStartingShip()
    {
        FailingSaveProgressService storage =
            new(loadError: new IOException("the save is locked"))
            {
                Content = """{ "PlayerX": 11, "PlayerY": 22, "ShipId": "earned", "Quests": [] }""",
            };

        GameSession session = CreateSession(storage);
        await session.Continue();
        await session.StartNewGame();

        Assert.True(session.IsSavingProgress);
        Assert.Null(session.SaveError);
        Assert.Same(TestShipYard.Starter, session.Player.Ship);
        Assert.Equal(TestShipYard.Starter.Id, SaveGameSerializer.Deserialize(storage.Content)!.ShipId);
    }

    // ---- the yard that hands the session nothing ----

    /// <summary>
    /// A yard with no starting ship is a content defect, and it has to surface here — named, at the
    /// award — rather than as a null reference on the frame something tries to draw the player's
    /// ship, which is a long way from the cause and looks like a rendering bug.
    /// </summary>
    [Fact]
    public async Task AYardWithNoStartingShip_FailsAtTheAward_NotAtTheDrawSite()
    {
        GameSession session = CreateSession(ships: new EmptyShipYard());

        ArgumentNullException error =
            await Assert.ThrowsAsync<ArgumentNullException>(session.StartNewGame);

        Assert.Equal("ship", error.ParamName);
    }

    // ---- what the write path does with an awkward identifier ----

    /// <summary>
    /// Identifiers are content, and content authors write what they like. Whatever a ship is called
    /// has to survive the trip through JSON unchanged, because the value that comes back is matched
    /// ordinally against the value that went out — an escape that does not round-trip exactly would
    /// silently unship the player's hull on the next load.
    /// </summary>
    [Theory]
    [InlineData("ship\"quoted")]
    [InlineData("ship\\slashed")]
    [InlineData("ship\nnewline")]
    [InlineData("ship nul")]
    [InlineData("shipéaccented")]
    [InlineData("宇宙船")]
    [InlineData("🚀")]
    [InlineData("ship‮rtl")]
    public async Task AnAwkwardIdentifier_SurvivesTheSaveExactly(string shipId)
    {
        Ship odd = new(shipId, "art", new ShipHandling(Acceleration: 1, Drag: 1, TurnRate: 1));

        GameSession session = CreateSession();
        await session.Continue();
        session.Player.Award(odd);
        await session.Save();

        SaveGame? reloaded = SaveGameSerializer.Deserialize(_saves.Content);

        Assert.NotNull(reloaded);
        Assert.Equal(shipId, reloaded.ShipId);
        Assert.Equal(shipId.Length, reloaded.ShipId.Length);
    }

    /// <summary>
    /// Starting over is durable, not just immediate. The earlier pass showed a new game takes the
    /// earned ship back in memory; this shows the decision reaches the file, so the next resume
    /// does not hand it straight back.
    /// </summary>
    [Fact]
    public async Task StartingOver_TakesTheEarnedShipBackForGood()
    {
        _saves.Content = """
            { "PlayerX": 0, "PlayerY": 500, "ShipId": "earned", "Quests": [] }
            """;

        GameSession first = CreateSession();
        await first.Continue();
        Assert.Same(TestShipYard.Earned, first.Player.Ship);

        await first.StartNewGame();

        GameSession resumed = CreateSession();
        await resumed.Continue();

        Assert.Same(TestShipYard.Starter, resumed.Player.Ship);
        Assert.Equal(Start, resumed.Player.Position);
    }
}
