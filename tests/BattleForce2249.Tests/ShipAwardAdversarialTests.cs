using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial pass over the ship award and the save that remembers it, carried across from
/// the coverage they parked on <c>claude/magical-franklin-aoeqge</c> when four pull requests were
/// open for one issue. Every test here is an attempt to break the claims #5 was handed off on: that
/// a new game awards a ship, that the save records the identifier and nothing else, and that a save
/// naming a ship this build does not have leaves the player downgraded rather than grounded.
/// </summary>
/// <remarks>
/// The interesting cases are all on the resume path, because that is the only place a value the
/// game did not write can reach the session. A save file is the one input to this feature that an
/// adversary — or an older build, or a hand edit — controls. The saves here are written as text
/// rather than through <see cref="SaveGameSerializer.Serialize"/> for the same reason: the resume
/// path has to survive shapes the current build would never write.
/// </remarks>
public sealed class ShipAwardAdversarialTests
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
            AutoStarts: true);

    readonly InMemorySaveProgressService _saves = new();

    GameSession CreateSession() =>
        new(_saves,
            new TestCampaign(Quest("quest-1")),
            new TestWorld(new QuestMarkers("quest-1", Start, End)),
            new TestShipYard());

    /// <summary>
    /// Writes a save file by hand, the way a previous build or a text editor would.
    /// </summary>
    async Task GivenSaveFile(string json) => await _saves.Save(json);

    // ---- the fallback: a save naming a ship this build cannot resolve ----

    [Theory]
    [InlineData("no-such-ship")]      // a hull a later build shipped and this one does not
    [InlineData("")]                  // written before ships were recorded
    [InlineData(" ")]                 // whitespace is not an identifier
    [InlineData("   \t  ")]
    [InlineData("STARTER")]           // identifiers are ordinal: case is not a near miss
    [InlineData("Starter")]
    [InlineData("starter ")]          // a stray space from a hand edit
    [InlineData(" starter")]
    [InlineData("starter\0")]         // an embedded NUL must not truncate to a match
    [InlineData("../../etc/passwd")]  // an identifier is not a path
    [InlineData("{ \"Id\": \"starter\" }")]
    public async Task ASaveNamingAnUnresolvableShip_LeavesThePlayerDowngraded_NotGrounded(string shipId)
    {
        await GivenSaveFile($$"""
            { "PlayerX": 5, "PlayerY": 7, "ShipId": {{System.Text.Json.JsonSerializer.Serialize(shipId)}}, "Quests": [] }
            """);

        GameSession session = CreateSession();
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Same(TestShipYard.Starter, session.Player.Ship);

        // the rest of the save is still honoured — an unknown ship must not discard the position
        Assert.Equal(5, session.Player.Position.X, 6);
        Assert.Equal(7, session.Player.Position.Y, 6);
    }

    [Fact]
    public async Task ASaveWithShipIdWrittenAsJsonNull_Loads()
    {
        // a property initialiser runs when the property is ABSENT, not when it is present and
        // null, so this is a genuinely different path from the older-save case. It is also the
        // one most likely to throw: IShipYard.Find takes a non-nullable string.
        await GivenSaveFile("""{ "PlayerX": 1, "PlayerY": 2, "ShipId": null, "Quests": null }""");

        GameSession session = CreateSession();
        await session.Continue();

        Assert.Same(TestShipYard.Starter, session.Player.Ship);

        // a null quest list restores nothing rather than throwing, so the campaign's own quests —
        // registered by Reset — are all that is left standing
        Assert.Equal(["quest-1"], session.Quests.Capture().Select(quest => quest.QuestId));
    }

    [Fact]
    public async Task AnUnresolvableShipId_IsCorrectedInTheNextSave_NotPreserved()
    {
        // the fallback has to stick: if the bad id survives the round trip the player is
        // downgraded again on every single load, and the save never heals
        await GivenSaveFile("""{ "PlayerX": 0, "PlayerY": 0, "ShipId": "ghost", "Quests": [] }""");

        GameSession session = CreateSession();
        await session.Continue();
        await session.Save();

        Assert.Equal("starter", _saves.Saved!.ShipId);
    }

    [Fact]
    public async Task ACorruptSave_StartsANewGameWithTheStartingShip()
    {
        await GivenSaveFile("{ this is not json");

        GameSession session = CreateSession();
        await session.Continue();

        Assert.True(session.IsReady);
        Assert.Same(TestShipYard.Starter, session.Player.Ship);
    }

    // ---- orderings ----

    [Fact]
    public async Task ContinuingTwiceOnOneSession_IsStable()
    {
        await GivenSaveFile("""{ "PlayerX": 0, "PlayerY": 0, "ShipId": "earned", "Quests": [] }""");

        GameSession session = CreateSession();
        await session.Continue();
        await session.Continue();

        Assert.Same(TestShipYard.Earned, session.Player.Ship);
        Assert.True(session.IsReady);
    }

    [Fact]
    public async Task TheRestoredShipLandsAfterTheReset_SoResumingDoesNotSilentlyDemote()
    {
        // Reset awards the starting ship and Continue then restores the saved one. Get the order
        // wrong and every resume quietly hands the player the starter back.
        await GivenSaveFile("""{ "PlayerX": 0, "PlayerY": 900, "ShipId": "earned", "Quests": [] }""");

        GameSession session = CreateSession();
        await session.Continue();

        Assert.Same(TestShipYard.Earned, session.Player.Ship);
        Assert.Equal(900, session.Player.Position.Y, 6);
    }

    // ---- what the save is allowed to contain ----

    [Fact]
    public async Task ASaveWrittenUnderTurkishCulture_ResolvesUnderInvariant()
    {
        // the classic dotted-I trap: if any lookup on the way in or out ever became culture-aware,
        // an identifier written under tr-TR would stop resolving elsewhere
        string json;
        using (new CultureScope("tr"))
        {
            json = SaveGameSerializer.Serialize(new SaveGame { ShipId = "STARTER".ToLowerInvariant() });
        }

        using (new CultureScope("en-GB"))
        {
            await GivenSaveFile(json);

            GameSession session = CreateSession();
            await session.Continue();

            Assert.Same(TestShipYard.Starter, session.Player.Ship);
        }
    }

    [Fact]
    public void ASaveIsMatchedOnPropertyNamesTheBuildWrote_InEitherDirection()
    {
        // the serialiser does not match property names case-insensitively, and does so
        // consistently — a miscased ShipId is ignored exactly as a miscased PlayerX is. Pinned
        // because "the ship went missing" would otherwise look ship-specific.
        SaveGame? loaded = SaveGameSerializer.Deserialize(
            """{ "playerx": 5, "shipid": "earned", "Quests": [] }""");

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.PlayerX);
        Assert.Equal("", loaded.ShipId);
    }
}
