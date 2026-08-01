using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's second adversarial pass over the ship award, run against the consolidated pull request for
/// #5. <see cref="ShipAwardAdversarialTests"/> already went after the resume path with hostile save
/// files and found nothing; this one goes after the surfaces that pass did not reach — the paths
/// that never touch a save file at all.
/// </summary>
/// <remarks>
/// <para>
/// Three seams are the target here, chosen because each is a claim the handoff makes that no test
/// currently holds:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>Capture</c>'s <c>Player.Ship?.Id ?? ""</c> is unreachable through any tested path, because
/// every tested path awards a ship before saving. If it is dead code the null-conditional is
/// noise; if it is reachable it is load-bearing and untested. It is reachable.
/// </description>
/// </item>
/// <item>
/// <description>
/// The composition root now derives <see cref="ShipHandling"/> from
/// <see cref="IShipYard.StartingShip"/> rather than registering it beside the yard. That is only
/// worth anything if a host substituting its own yard moves both, which nothing checks.
/// </description>
/// </item>
/// <item>
/// <description>
/// Every session test that exercises the ship uses <see cref="TestShipYard"/>, deliberately, so
/// that awarding and restoring are distinguishable. The consequence is that no test flies the
/// configuration that actually ships.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class ShipAwardSecondPassAdversarialTests
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

    static GameSession CreateSession(ISaveProgressService saves, IShipYard ships) =>
        new(saves,
            new TestCampaign(Quest("quest-1")),
            new TestWorld(new QuestMarkers("quest-1", Start, End)),
            ships);

    GameSession CreateSession(IShipYard? ships = null) =>
        CreateSession(_saves, ships ?? new TestShipYard());

    // ---- saving before there is a ship to save ----

    [Fact]
    public async Task Save_BeforeAGameHasStarted_WritesNoShipRatherThanThrowing()
    {
        // IGameSession.Save is public and nothing gates it on IsReady, so a caller can reach it on
        // a session that has neither started nor resumed — the frame between construction and the
        // menu choosing. Player.Ship is null there, which is exactly what Capture's null-conditional
        // is for. Without this test that operator is unproven: nothing else can reach it.
        GameSession session = CreateSession();

        await session.Save();

        Assert.Equal("", _saves.Saved!.ShipId);
    }

    [Fact]
    public async Task ASaveWrittenBeforeAShipWasAwarded_ResumesIntoTheStartingShip()
    {
        // and the empty identifier that writes has to survive the round trip, because it is
        // indistinguishable from a save written by a build from before ships were recorded
        GameSession writer = CreateSession();
        await writer.Save();

        GameSession reader = CreateSession();
        await reader.Continue();

        Assert.True(reader.IsReady);
        Assert.Same(TestShipYard.Starter, reader.Player.Ship);
    }

    // ---- the save that could not be read ----

    [Fact]
    public async Task AShipAwardedOverALockedSave_IsNotWrittenBackOverIt()
    {
        // Reset awards the starting ship on the storage-failure path so the player has something to
        // fly. That award must not become a write: the save was locked, not lost, and overwriting
        // it with a fresh game's starting ship would destroy a save that was never actually gone.
        // The award and the write-suppression are set by the same method, one line apart, which is
        // how a change to one quietly takes the other with it.
        FailingSaveProgressService saves = new(loadError: new IOException("locked"))
        {
            Content = SaveGameSerializer.Serialize(new SaveGame { ShipId = "earned" }),
        };
        GameSession session = CreateSession(saves, new TestShipYard());

        await session.Continue();

        Assert.NotNull(session.Player.Ship);
        Assert.False(session.IsSavingProgress);

        // and it stays suppressed once the game is actually being played
        session.Quests.Find("quest-1")!.Start();
        await session.PendingSave;

        Assert.Equal(0, saves.SaveCount);
    }

    // ---- the configuration that actually ships ----

    [Fact]
    public async Task TheShippingYard_RoundTripsItsOwnShipThroughARealSaveAndBack()
    {
        // every other session test substitutes TestShipYard, on purpose, so that awarding and
        // restoring give different answers. The cost is that nothing exercises the real pairing:
        // BattleForceShip.Id is a const and BattleForceShip.Ship is built from it, but the yard
        // finds by comparing against Ship.Id, so a future hand-written yard entry that disagreed
        // with the const would leave the shipping build unable to resume its own save.
        GameSession writer = CreateSession(_saves, new BattleForceShipYard());
        await writer.StartNewGame();

        Assert.Equal("disgraced", _saves.Saved!.ShipId);

        GameSession reader = CreateSession(_saves, new BattleForceShipYard());
        await reader.Continue();

        Assert.Same(BattleForceShip.Ship, reader.Player.Ship);
        Assert.Equal("ship1", reader.Player.Ship!.AssetKey);
    }

    // ---- a ship the yard does not stock, awarded by game code rather than read from a save ----

    [Fact]
    public async Task AShipTheYardDoesNotStock_SurvivesUntilTheReload_ThenDowngrades()
    {
        // IGameSession.Player is public and Player.Award takes any Ship, so game code can put the
        // player in a hull the yard never heard of. The save records the identifier only, so the
        // yard is the sole authority on the way back in — and the downgrade must not take the
        // player's position with it.
        GameSession writer = CreateSession();
        await writer.StartNewGame();
        writer.Player.Award(new Ship("prototype", "prototype-art", new ShipHandling(1, 1, 1)));
        writer.Player.MoveTo(new Position(11, 22));
        await writer.Save();

        Assert.Equal("prototype", _saves.Saved!.ShipId);

        GameSession reader = CreateSession();
        await reader.Continue();

        Assert.Same(TestShipYard.Starter, reader.Player.Ship);
        Assert.Equal(new Position(11, 22), reader.Player.Position);
    }

    // ---- identifiers that only look like the ship's ----

    [Theory]
    [InlineData("disgrаced")]        // Cyrillic а (U+0430) for the Latin one
    [InlineData("disgraced​")]  // a zero-width space off the end of a copy-paste
    [InlineData("﻿disgraced")]  // a byte order mark off the front of a hand-edited file
    [InlineData("ｄisgraced")]        // full-width d
    [InlineData("disgraced\n")]
    [InlineData("dis graced")]
    public void AnIdentifierThatOnlyLooksLikeTheShips_IsNotTheShip(string shipId)
    {
        // the yard matches ordinal, which is what stops case folding putting an unearned hull in
        // the cockpit. The same guarantee has to hold for the confusions a save file can actually
        // pick up — a homoglyph, an invisible character, a stray newline — because none of those
        // are near misses to a byte comparison and all of them are near misses to a human.
        Assert.Null(new BattleForceShipYard().Find(shipId));
    }

    [Fact]
    public void TheYard_IsHandedNothing_AndSaysItHasNothing()
    {
        // IShipYard.Find is declared non-nullable, and GameSession only ever reaches it with an
        // identifier the serialiser has already coerced. Nothing enforces either at runtime, and a
        // yard that threw here would turn another game's null into a crash on load rather than a
        // downgrade. Refusing to match is the failure worth having.
        Assert.Null(new BattleForceShipYard().Find(null!));
    }

    // ---- a yard that does not hold up its end ----

    sealed class EmptyShipYard : IShipYard
    {
        public IReadOnlyList<Ship> Ships { get; } = [];

        public Ship StartingShip => null!;

        public Ship? Find(string shipId) => null;
    }

    [Fact]
    public async Task AYardWithNothingToAward_FailsLoudlyRatherThanLeavingThePlayerFlyingNothing()
    {
        // IShipYard is the seam another game implements, and StartingShip is declared non-nullable
        // without anything enforcing it. A yard that hands back nothing must not produce a ready
        // session with a null Ship: #16 is about to dereference that to draw, and a null reference
        // at the draw site is a long way from the yard that caused it. Player.Award's guard is what
        // turns it into a failure at the point of the mistake.
        GameSession session = CreateSession(new EmptyShipYard());

        await Assert.ThrowsAsync<ArgumentNullException>(() => session.StartNewGame());

        Assert.False(session.IsReady);
        Assert.Null(session.Player.Ship);
    }

    // ---- unexpected orderings ----

    [Fact]
    public async Task ASaveThatBecomesReadable_RestoresTheShipTheSecondTimeRound()
    {
        // the locked-save path leaves a playable game with the starting ship and saving suppressed,
        // on the reasoning that the file may be free again later. If the retry does not then pick
        // the earned ship back up, that reasoning never pays off and the player keeps the downgrade
        // for the rest of the session.
        LockOnceSaveProgressService saves = new()
        {
            Content = SaveGameSerializer.Serialize(new SaveGame { PlayerY = 400, ShipId = "earned" }),
        };
        GameSession session = CreateSession(saves, new TestShipYard());

        await session.Continue();
        Assert.Same(TestShipYard.Starter, session.Player.Ship);

        await session.Continue();

        Assert.Same(TestShipYard.Earned, session.Player.Ship);
        Assert.Equal(400, session.Player.Position.Y, 6);
        Assert.Null(session.SaveError);
    }

    /// <summary>
    /// A store whose first read fails and whose later reads succeed — the file that was locked for
    /// a moment rather than lost.
    /// </summary>
    sealed class LockOnceSaveProgressService : ISaveProgressService
    {
        bool _thrown;

        public string? Content { get; set; }

        public Task<bool> HasProgress() => Task.FromResult(Content is not null);

        public Task<string?> Load()
        {
            if (_thrown)
            {
                return Task.FromResult(Content);
            }

            _thrown = true;
            throw new IOException("locked");
        }

        public Task Save(string content)
        {
            Content = content;
            return Task.CompletedTask;
        }
    }

    // ---- the seam a host substitutes through ----

    [Fact]
    public void AHostSubstitutingItsOwnYard_FliesItsOwnShipsNumbers()
    {
        // the composition root derives ShipHandling from IShipYard.StartingShip rather than
        // registering it beside the yard, so that the owned ship and the flown ship cannot be
        // given two different sets of numbers. That is only true if the derivation is late: a
        // factory resolving the yard when the provider is built follows a substitution, and an
        // eagerly captured instance does not. Nothing else distinguishes the two.
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce();

        // after AddBattleForce, the way a host registering a real input device does it
        TestShipYard yard = new();
        services.AddSingleton<IShipYard>(yard);

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.Same(yard, provider.GetRequiredService<IShipYard>());
        Assert.Same(TestShipYard.Starter.Handling, provider.GetRequiredService<ShipHandling>());
    }
}
