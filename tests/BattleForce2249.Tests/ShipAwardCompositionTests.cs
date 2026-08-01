using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// What is left of QA's pass over the ship award once
/// <see cref="ShipAwardAdversarialTests"/>, <see cref="ShipAwardBoundaryTests"/> and
/// <see cref="ShipAwardSecondPassAdversarialTests"/> have had the save file: the composition root
/// the shipping yard is actually wired through, and the asset the ship claims to be drawn as.
/// </summary>
/// <remarks>
/// Everything else on this feature is exercised against a substituted yard, on purpose, so that
/// "the ship a new game awards" and "the ship the save named" are different answers. The cost is
/// that the wiring the game really launches with — <c>AddBattleForce</c> registering
/// <see cref="BattleForceShipYard"/>, and the asset key naming a file the content build produces —
/// is the one path nothing walks end to end.
/// </remarks>
public sealed class ShipAwardCompositionTests
{
    [Fact]
    public async Task TheShippingGame_AwardsTheDisgracedsShip_ThroughItsOwnCompositionRoot()
    {
        // #5 through the real object graph: the session AddBattleForce registers, resolving the
        // yard AddBattleForce registers, awarding the ship that yard stocks. Constructing a
        // GameSession by hand around a real yard proves the pairing but not the registration, so a
        // yard wired to the wrong lifetime — or not wired at all — would still pass. Only the save
        // service is substituted, so the test does not write to the player's save file.
        InMemorySaveProgressService saves = new();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce();
        services.AddSingleton<ISaveProgressService>(saves);

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        IGameSession session = provider.GetRequiredService<IGameSession>();
        await session.StartNewGame();

        Assert.True(session.IsReady);
        Assert.Same(BattleForceShip.Ship, session.Player.Ship);
        Assert.Equal("ship1", session.Player.Ship?.AssetKey);
        Assert.Equal("disgraced", saves.Saved?.ShipId);
    }

    [Fact]
    public void TheStartingShipsAssetKey_NamesAnAssetTheContentBuildProduces()
    {
        // #5 asks for ship1.png specifically, and every other test on the asset key asserts the
        // string against itself — BattleForceShip.AssetKey against the literal "ship1", or the
        // yard's ship against the same const. A typo, or an asset renamed in the content build and
        // not here, satisfies all of them and fails only when the game is launched and the player
        // is given a ship that draws as nothing. The content build is the other end of that
        // identifier and nothing was checking it.
        string mgcb = Path.Combine(RepositoryRoot(), "BattleForce2249.MonoGame", "Content", "Content.mgcb");
        Assert.True(File.Exists(mgcb), $"the content build was not found at '{mgcb}'");

        string assetKey = new BattleForceShipYard().StartingShip.AssetKey;

        Assert.Contains($"/build:{assetKey}.png", File.ReadAllText(mgcb), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartNewGame_WhenTheSaveCannotBeWritten_StillLeavesThePlayerInAShip()
    {
        // the read failing is covered; the write failing is the other half and reaches different
        // code. StartNewGame awards, then saves, and the save throwing must not leave the session
        // ready with nothing to fly — whatever draws the player dereferences Player.Ship on the
        // strength of IsReady alone.
        FailingSaveProgressService full = new(saveError: new IOException("no space left on device"));

        GameSession session = new(
            full,
            new BattleForceCampaign(),
            new BattleForceWorld(),
            new BattleForceShipYard());

        await session.StartNewGame();

        Assert.True(session.IsReady);
        Assert.Same(BattleForceShip.Ship, session.Player.Ship);
        Assert.NotNull(session.SaveError);
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding the solution, so the path does not
    /// depend on how deep the build output happens to be nested.
    /// </summary>
    static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OliveGameStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
