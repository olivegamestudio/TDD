using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers where things stand in the world, and the one fact that spans the world and the campaign:
/// that quest 1's start marker is close enough to the spawn point to fire its trigger.
/// </summary>
public sealed class BattleForceWorldTests
{
    readonly BattleForceWorld _world = new();

    readonly BattleForceCampaign _campaign = new();

    QuestMarkers Quest1Markers =>
        _world.QuestMarkers.Single(markers => markers.QuestId == BattleForceCampaign.Quest1Id);

    QuestDefinition Quest1 =>
        _campaign.Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    [Fact]
    public void PlacesMarkersForQuest1()
    {
        Assert.Contains(_world.QuestMarkers, markers => markers.QuestId == BattleForceCampaign.Quest1Id);
    }

    [Fact]
    public void Quest1_StartsWithinReachOfWhereANewGamePutsThePlayer()
    {
        // this is what makes quest 1 begin on a new game launch; if the spawn point drifted out of
        // range the quest would silently never start
        Assert.True(_world.PlayerStart.DistanceTo(Quest1Markers.Start) <= Quest1.Start.Distance);
    }

    [Fact]
    public void Quest1_EndsForwardOfItsStart()
    {
        Assert.True(Quest1Markers.End.Y > Quest1Markers.Start.Y);
    }

    [Fact]
    public void Quest1_EndsOutOfReachOfTheSpawnPoint()
    {
        // otherwise the quest would complete the instant it started, and there would be no game
        Assert.True(_world.PlayerStart.DistanceTo(Quest1Markers.End) > Quest1.End.Distance);
    }

    [Fact]
    public void EveryQuestInTheCampaignHasMarkers()
    {
        Assert.All(
            _campaign.Quests,
            quest => Assert.Contains(_world.QuestMarkers, markers => markers.QuestId == quest.Id));
    }

    // ---- bringing a ship into the world ----

    static Ship AShip() => new(DisgracedShip.Profile);

    [Fact]
    public void AShipIntroducedAtTheMines_StandsWhereANewGameBegins()
    {
        // the world is the placement authority: content names the place, the world says where that
        // is. Stated once, so the mines and the spawn point cannot drift apart.
        Assert.Equal(_world.PlayerStart, _world.Introduce(AShip(), BattleForceWorld.Mines));
    }

    [Fact]
    public void TheMinesAreWhereQuest1Begins()
    {
        // the place the Disgraced starts is the place the opening quest is about escaping, so a
        // ship put down there is already within reach of quest 1's start marker
        Position placed = _world.Introduce(AShip(), BattleForceWorld.Mines);

        Assert.True(placed.DistanceTo(Quest1Markers.Start) <= Quest1.Start.Distance);
    }

    [Fact]
    public void APlaceTheWorldDoesNotHave_IsRefused()
    {
        // a mistake in content rather than something a player did, so it fails where the content is
        // read instead of stranding the player at coordinates nobody chose
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _world.Introduce(AShip(), "somewhere-else"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void APlaceNothingCanName_IsRefused(string? location)
    {
        Assert.ThrowsAny<ArgumentException>(() => _world.Introduce(AShip(), location!));
    }

    [Fact]
    public void IntroducingNoShip_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => _world.Introduce(null!, BattleForceWorld.Mines));
    }
}
