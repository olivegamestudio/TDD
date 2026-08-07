namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers what <see cref="Loot"/> does when the hold it collects into has no room: that nothing is
/// destroyed on the player's behalf, and that a guarantee which cannot be kept says so.
/// </summary>
public sealed class LootFullHoldTests
{
    static readonly Item Salvage = new("salvage.plating", ItemStats.Single);
    static readonly Item Ore = new("ore.iron", ItemStats.Single);

    static Loot AMagnet(double reach = 50, double driftSpeed = 10) =>
        new(new LootMagnet(reach, driftSpeed));

    static Inventory FullHold()
    {
        Inventory held = new(slots: 1);
        held.Add(Ore);
        return held;
    }

    [Fact]
    public void ADropArrivingAtAFullHold_StaysInTheWorldRatherThanEvaporatingOnContact()
    {
        Loot loot = AMagnet();
        Inventory held = FullHold();

        LootDrop drop = loot.Drop(Salvage, new Position(0, 10));
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 10, held);

        Assert.Equal([drop], loot.Drops);
        Assert.False(held.Contains(Salvage));
        Assert.Equal(1, held.Count);
    }

    [Fact]
    public void ADropWaitingOnAFullHold_IsTakenOnTheFirstFrameAfterRoomIsMade()
    {
        // it has already been drawn in, so it sits on the player and is collected the moment they
        // make room — never on no frame at all
        Loot loot = AMagnet();
        Inventory held = FullHold();

        loot.Drop(Salvage, new Position(0, 10));
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 10, held);

        held.Remove(Ore);
        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 10, held);

        Assert.Empty(loot.Drops);
        Assert.Equal([Salvage], held.Items);
    }

    [Fact]
    public void ADropThereIsNoRoomFor_DoesNotStopTheOnesBehindItBeingCollected()
    {
        // the pile is walked in the order it fell, and one item with nowhere to go must not swallow
        // the frame's other arrivals
        Item scrap = new("ore.scrap", new ItemStats(EquipSlot.None, StackLimit: 5));

        Loot loot = AMagnet();
        Inventory held = new(slots: 1);
        held.Add(scrap);

        // the hold's one slot is a part-filled stack of scrap: there is room for more scrap and
        // room for nothing else
        LootDrop refused = loot.Drop(Salvage, new Position(0, 10));
        loot.Drop(scrap, new Position(0, 12));

        loot.Update(Position.Origin, Position.Origin, elapsedSeconds: 10, held);

        Assert.Equal([refused], loot.Drops);
        Assert.Equal(2, held.CountOf(scrap));
        Assert.False(held.Contains(Salvage));
    }

    [Fact]
    public void AGuaranteedDrop_IsCollectedTheInstantItFalls()
    {
        Loot loot = AMagnet();
        Inventory held = new(slots: 2);

        Assert.True(loot.DropGuaranteed(Salvage, held));

        Assert.Equal([Salvage], held.Items);
        Assert.Empty(loot.Drops);
    }

    [Fact]
    public void AGuaranteedDropIntoAFullHold_SaysItCouldNotBeKeptRatherThanLosingTheItem()
    {
        // dropping it into the world instead would put the guarantee back exactly where the caller
        // asked for it not to be — reachable, and therefore missable
        Loot loot = AMagnet();
        Inventory held = FullHold();

        Assert.False(loot.DropGuaranteed(Salvage, held));

        Assert.Empty(loot.Drops);
        Assert.False(held.Contains(Salvage));
    }
}
