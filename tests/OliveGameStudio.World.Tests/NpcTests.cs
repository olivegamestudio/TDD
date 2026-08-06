namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers somebody standing in the world: what they are allowed to be, whether they move, and how
/// close counts as close enough to talk to them.
/// </summary>
public sealed class NpcTests
{
    static Conversation AConversation() =>
        new("miner", [new ConversationLine("MinerName", "MinerGreeting")]);

    static Npc AnNpc(Position at, double range = 50, NpcPatrol? patrol = null) =>
        new("miner", at, AConversation(), range, patrol);

    [Fact]
    public void AnNpcKeepsWhatItWasGiven()
    {
        Conversation conversation = AConversation();
        Npc npc = new("miner", new Position(3, 4), conversation, 50);

        Assert.Equal("miner", npc.Id);
        Assert.Equal(new Position(3, 4), npc.Position);
        Assert.Same(conversation, npc.Conversation);
        Assert.Equal(50, npc.InteractionRange);
    }

    [Fact]
    public void SomebodyWithNoRoute_IsStatic()
    {
        Assert.True(AnNpc(Position.Origin).IsStatic);
    }

    [Fact]
    public void SomebodyWithARoute_IsNot()
    {
        Npc npc = AnNpc(Position.Origin, patrol: new NpcPatrol(10, [new Position(0, 100)]));

        Assert.False(npc.IsStatic);
    }

    [Fact]
    public void AStaticNpc_IsWhereItWasPutHoweverLongPasses()
    {
        Npc npc = AnNpc(new Position(3, 4));

        npc.Update(TimeSpan.FromSeconds(10));

        Assert.Equal(new Position(3, 4), npc.Position);
    }

    [Fact]
    public void AWalkingNpc_WalksItsRoute()
    {
        Npc npc = AnNpc(Position.Origin, patrol: new NpcPatrol(10, [new Position(0, 100)]));

        npc.Update(TimeSpan.FromSeconds(2));

        Assert.Equal(new Position(0, 20), npc.Position);
    }

    [Fact]
    public void UpdatingRefusesTimeRunningBackwards()
    {
        Npc npc = AnNpc(Position.Origin);

        Assert.Throws<ArgumentOutOfRangeException>(() => npc.Update(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void SomebodyInsideTheRange_CanBeTalkedTo()
    {
        Assert.True(AnNpc(Position.Origin, range: 50).IsWithinReach(new Position(0, 49)));
    }

    [Fact]
    public void SomebodyExactlyAtTheRange_CanBeTalkedTo()
    {
        // the range is how close you have to be, so standing exactly there is close enough
        Assert.True(AnNpc(Position.Origin, range: 50).IsWithinReach(new Position(0, 50)));
    }

    [Fact]
    public void SomebodyOutsideTheRange_CannotBe()
    {
        Assert.False(AnNpc(Position.Origin, range: 50).IsWithinReach(new Position(0, 51)));
    }

    [Fact]
    public void ReachIsMeasuredFromWhereTheyHaveWalkedTo()
    {
        Npc npc = AnNpc(Position.Origin, range: 5, patrol: new NpcPatrol(10, [new Position(0, 100)]));

        npc.Update(TimeSpan.FromSeconds(5));

        Assert.False(npc.IsWithinReach(new Position(0, 0)));
        Assert.True(npc.IsWithinReach(new Position(0, 50)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnNpcRefusesNoIdentity(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Npc(id!, Position.Origin, AConversation(), 50));
    }

    [Fact]
    public void AnNpcRefusesHavingNothingToSay()
    {
        Assert.Throws<ArgumentNullException>(() => new Npc("miner", Position.Origin, null!, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnNpcRefusesARangeNobodyCanSatisfy(double range)
    {
        // zero would need the player at exactly the same point, so nobody could ever talk to them
        // and nothing would say why
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Npc("miner", Position.Origin, AConversation(), range));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnNpcRefusesARangeThatIsNotANumber(double range)
    {
        // an infinite one is satisfied everywhere, which makes every NPC in the world the one you
        // happen to be standing next to
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Npc("miner", Position.Origin, AConversation(), range));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnNpcRefusesStandingSomewhereThatIsNotAPlace(double coordinate)
    {
        Assert.Throws<ArgumentException>(
            () => new Npc("miner", new Position(coordinate, 0), AConversation(), 50));
        Assert.Throws<ArgumentException>(
            () => new Npc("miner", new Position(0, coordinate), AConversation(), 50));
    }
}
