namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Loot"/>: that a drop lies where it fell, that coming near it draws it in and
/// it arrives without a button, that the journey a frame covered is what reaches it, and that a
/// guaranteed drop cannot be missed at all.
/// </summary>
public sealed class LootTests
{
    static readonly Item Salvage = new("salvage.plating", new ItemStats(EquipSlot.None, StackLimit: 10));
    static readonly Item Ore = new("ore.iron", new ItemStats(EquipSlot.None, StackLimit: 10));

    static Loot AMagnet(double reach = 50, double driftSpeed = 10) =>
        new(new LootMagnet(reach, driftSpeed));

    [Fact]
    public void ADroppedItem_LiesWhereItFell()
    {
        Loot loot = AMagnet();

        LootDrop drop = loot.Drop(Salvage, new Position(10, 20));

        Assert.Same(Salvage, drop.Item);
        Assert.Equal(new Position(10, 20), drop.Position);
        Assert.False(drop.Attracted);
        Assert.Equal([drop], loot.Drops);
    }

    [Fact]
    public void DropsInTheWorld_AreHeldInTheOrderTheyFell()
    {
        Loot loot = AMagnet();

        LootDrop first = loot.Drop(Salvage, new Position(0, 100));
        LootDrop second = loot.Drop(Ore, new Position(0, 200));

        Assert.Equal([first, second], loot.Drops);
    }

    [Fact]
    public void ADropThePlayerNeverComesNear_StaysWhereItIs()
    {
        Loot loot = AMagnet(reach: 50);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(0, 500));
        loot.Update(Position.Origin, new Position(0, 100), elapsedSeconds: 1, held);

        Assert.False(drop.Attracted);
        Assert.Equal(new Position(0, 500), drop.Position);
        Assert.Empty(held.Items);
        Assert.Equal([drop], loot.Drops);
    }

    [Fact]
    public void ADropThePlayerComesWithinReachOf_IsDrawnInWithoutAButton()
    {
        Loot loot = AMagnet(reach: 50, driftSpeed: 10);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(0, 20));
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Assert.True(drop.Attracted);
    }

    [Fact]
    public void ADropDrawnIn_DriftsTowardThePlayerRatherThanJumpingToThem()
    {
        Loot loot = AMagnet(reach: 50, driftSpeed: 10);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(0, 20));

        // one second of drift at ten units a second covers half of the twenty it has to travel
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Assert.Equal(new Position(0, 10), drop.Position);
        Assert.Empty(held.Items);
        Assert.Equal([drop], loot.Drops);
    }

    [Fact]
    public void ADropThatReachesThePlayer_IsCollectedAndLeavesTheWorld()
    {
        Loot loot = AMagnet(reach: 50, driftSpeed: 10);
        Inventory held = new(slots: 8);

        loot.Drop(Salvage, new Position(0, 20));

        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Assert.Equal([Salvage], held.Items);
        Assert.Empty(loot.Drops);
    }

    [Fact]
    public void ADropTheFrameFlewStraightPast_IsStillReached()
    {
        // Pillar 1: a trigger a fast ship flies through is a bug against flying feeling good, not
        // a tuning detail. The drop is a single unit from the line the frame covered and nowhere
        // near either end of it, so sampling the point the frame finished at would miss it.
        Loot loot = AMagnet(reach: 5, driftSpeed: 10);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(1, 500));
        loot.Update(Position.Origin, new Position(0, 1000), elapsedSeconds: 1, held);

        Assert.True(drop.Attracted);
    }

    [Fact]
    public void ADropAlreadyDrawnIn_KeepsComingWhenThePlayerFliesOutOfReach()
    {
        // Loot is collected with no button, so there is nothing the player can do to claim a drop
        // that gave up on them. One that has been drawn in keeps coming and arrives when they
        // slow down.
        Loot loot = AMagnet(reach: 50, driftSpeed: 10);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(0, 20));
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Position faraway = new(0, 10_000);
        loot.Update(Position.Origin, faraway, elapsedSeconds: 1, held);

        Assert.True(drop.Attracted);

        // still chasing them rather than having given up where it was left
        Assert.True(drop.Position.Y > 10);
        Assert.Equal([drop], loot.Drops);
    }

    [Fact]
    public void SeveralDropsCollectedInOneFrame_ArriveInTheOrderTheyFell()
    {
        Loot loot = AMagnet(reach: 50, driftSpeed: 100);
        Inventory held = new(slots: 8);

        loot.Drop(Salvage, new Position(0, 10));
        loot.Drop(Ore, new Position(0, 20));

        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Assert.Equal([Salvage, Ore], held.Items);
        Assert.Empty(loot.Drops);
    }

    [Fact]
    public void AGuaranteedDrop_IsCollectedTheInstantItFallsWhereverThePlayerIs()
    {
        Loot loot = AMagnet(reach: 1, driftSpeed: 1);
        Inventory held = new(slots: 8);

        loot.DropGuaranteed(Salvage, held);

        Assert.Equal([Salvage], held.Items);
        Assert.Empty(loot.Drops);
    }

    [Fact]
    public void APositionNothingCouldBeCollectedFrom_IsRefusedWhereItIsDropped()
    {
        // A drop nothing can measure a distance to is never drawn in and never collected: an item
        // lost the moment it fell. Refused where it is dropped rather than lying in the world
        // being skipped by every frame.
        Loot loot = AMagnet();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => loot.Drop(Salvage, new Position(double.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => loot.Drop(Salvage, new Position(0, double.PositiveInfinity)));
        Assert.Empty(loot.Drops);
    }

    [Fact]
    public void AFrameThatCannotBeMeasured_LeavesADropWhereItIsRatherThanLosingIt()
    {
        Loot loot = AMagnet(reach: 50, driftSpeed: 10);
        Inventory held = new(slots: 8);

        LootDrop drop = loot.Drop(Salvage, new Position(0, 20));
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Position nowhere = new(double.NaN, double.NaN);
        loot.Update(nowhere, nowhere, elapsedSeconds: 1, held);

        Assert.Equal(new Position(0, 10), drop.Position);
        Assert.Equal([drop], loot.Drops);

        // and it carries on as soon as a frame can be measured again
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, held);

        Assert.Equal([Salvage], held.Items);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ATimeNoDropCouldDriftFor_IsRefusedWhereTheFrameIsApplied(double elapsedSeconds)
    {
        Loot loot = AMagnet();
        Inventory held = new(slots: 8);

        loot.Drop(Salvage, new Position(0, 10));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => loot.Update(Position.Origin, Position.Origin, elapsedSeconds, held));
    }

    [Fact]
    public void AMagnetThatIsNothing_IsRefusedWhereTheLootIsMade() =>
        Assert.Throws<ArgumentNullException>(() => new Loot(null!));

    [Fact]
    public void AnItemOrAnInventoryThatIsNothing_IsRefusedWhereItIsHandedOver()
    {
        Loot loot = AMagnet();
        Inventory held = new(slots: 8);

        Assert.Throws<ArgumentNullException>(() => loot.Drop(null!, Position.Origin));
        Assert.Throws<ArgumentNullException>(() => loot.DropGuaranteed(null!, held));
        Assert.Throws<ArgumentNullException>(() => loot.DropGuaranteed(Salvage, null!));
        Assert.Throws<ArgumentNullException>(
            () => loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 1, null!));
    }
}
