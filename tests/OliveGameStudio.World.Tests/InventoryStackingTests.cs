namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers the slot rules <see cref="Inventory"/> holds items by: that items of a kind share a slot
/// up to their own limit, that a hold runs out, and that running out costs the player nothing they
/// were carrying.
/// </summary>
public sealed class InventoryStackingTests
{
    static readonly Item Ore = new("ore.iron", new ItemStats(EquipSlot.None, StackLimit: 3));
    static readonly Item Cell = new("power-cell", ItemStats.Single);

    [Fact]
    public void ItemsOfAKind_ShareASlot()
    {
        Inventory inventory = new(slots: 4);

        inventory.Add(Ore);
        inventory.Add(Ore);

        Assert.Equal(1, inventory.UsedSlots);
        Assert.Equal(2, inventory.CountOf(Ore));
        Assert.Equal(2, inventory.Count);
        Assert.Equal(3, inventory.FreeSlots);
    }

    [Fact]
    public void OneMoreThanAStackHolds_OpensAnotherSlot()
    {
        // the player feels the hold filling up, which is the whole point of a stack limit
        Inventory inventory = new(slots: 4);

        for (int held = 0; held < 4; held++)
        {
            Assert.True(inventory.Add(Ore));
        }

        Assert.Equal(2, inventory.UsedSlots);
        Assert.Equal([3, 1], inventory.Stacks.Select(stack => stack.Count));
        Assert.Equal(4, inventory.CountOf(Ore));
    }

    [Fact]
    public void ItemsThatDoNotStack_TakeASlotEach()
    {
        Inventory inventory = new(slots: 4);

        inventory.Add(Cell);
        inventory.Add(Cell);

        Assert.Equal(2, inventory.UsedSlots);
        Assert.Equal([Cell, Cell], inventory.Items);
    }

    [Fact]
    public void APartFilledStack_IsRoomEvenWhenEverySlotIsTaken()
    {
        Inventory inventory = new(slots: 1);

        inventory.Add(Ore);

        Assert.Equal(0, inventory.FreeSlots);
        Assert.True(inventory.HasRoomFor(Ore));
        Assert.True(inventory.Add(Ore));
        Assert.Equal(2, inventory.CountOf(Ore));
    }

    [Fact]
    public void AFullHold_RefusesWhatWillNotFitAndTakesNothing()
    {
        // being full is an answer rather than a failure: the caller still has the item, and nothing
        // is destroyed on the player's behalf
        Inventory inventory = new(slots: 1);

        for (int held = 0; held < 3; held++)
        {
            Assert.True(inventory.Add(Ore));
        }

        Assert.False(inventory.HasRoomFor(Ore));
        Assert.False(inventory.Add(Ore));
        Assert.False(inventory.Add(Cell));

        Assert.Equal(3, inventory.Count);
        Assert.Equal(1, inventory.UsedSlots);
        Assert.False(inventory.Contains(Cell));
    }

    [Fact]
    public void MakingRoom_LetsTheHoldTakeAgain()
    {
        Inventory inventory = new(slots: 1);
        inventory.Add(Cell);

        Assert.False(inventory.Add(Ore));

        inventory.Remove(Cell);

        Assert.True(inventory.Add(Ore));
    }

    [Fact]
    public void Removing_EmptiesThePartFilledSlotFirstSoASlotComesFree()
    {
        // taking from the front instead would leave a hold of full stacks with a gap at the start
        // and no more room than before
        Inventory inventory = new(slots: 4);
        for (int held = 0; held < 4; held++)
        {
            inventory.Add(Ore);
        }

        Assert.True(inventory.Remove(Ore));

        Assert.Equal(1, inventory.UsedSlots);
        Assert.Equal(3, inventory.CountOf(Ore));
        Assert.Equal(3, inventory.FreeSlots);
    }

    [Fact]
    public void ASlotThatEmpties_IsGivenUpRatherThanHeldOnTo()
    {
        // or a hold would fill up with nothing
        Inventory inventory = new(slots: 2);
        inventory.Add(Ore);

        Assert.True(inventory.Remove(Ore));

        Assert.Equal(0, inventory.UsedSlots);
        Assert.Equal(2, inventory.FreeSlots);
        Assert.Equal(0, inventory.CountOf(Ore));
    }

    [Fact]
    public void CountOf_AddsUpEverySlotHoldingTheItem()
    {
        Inventory inventory = new(slots: 4);
        for (int held = 0; held < 5; held++)
        {
            inventory.Add(Ore);
        }

        inventory.Add(Cell);

        Assert.Equal(5, inventory.CountOf(Ore));
        Assert.Equal(1, inventory.CountOf(Cell));
        Assert.Equal(6, inventory.Count);
    }

    [Fact]
    public void OneIdAuthoredTwiceWithDifferentNumbers_IsRefusedRatherThanStackedUnderOneOfThem()
    {
        // an item is its id, so this is one item described wrongly rather than two items; stacking
        // them together would hold one lot of them under the other's limit
        Inventory inventory = new(slots: 4);
        inventory.Add(Ore);

        Item sameIdDifferentStats = new("ore.iron", new ItemStats(EquipSlot.Weapon, StackLimit: 99));

        Assert.Throws<ArgumentException>(() => inventory.Add(sameIdDifferentStats));
        Assert.Equal(1, inventory.CountOf(Ore));
    }

    [Fact]
    public void AskingWhetherThereIsRoomForOneIdAuthoredTwice_IsRefusedTheWayAddingItIs()
    {
        // HasRoomFor exists to say what Add would do without doing it, so the two have to agree on
        // every item — including the one Add refuses outright. Answering "no room" here would be
        // the wrong no: the caller would leave the item in the world, read that as an ordinary full
        // hold, and never find out the content is misauthored.
        Inventory inventory = new(slots: 4);
        inventory.Add(Ore);

        Item sameIdDifferentStats = new("ore.iron", new ItemStats(EquipSlot.Weapon, StackLimit: 99));

        Assert.Throws<ArgumentException>(() => inventory.HasRoomFor(sameIdDifferentStats));
    }

    [Fact]
    public void AnItemAuthoredTwice_IsRefusedEvenWhenThereIsAnEmptySlotToOpen()
    {
        // the free slot is what hid this: HasRoomFor answered from FreeSlots before it ever looked
        // at what was already held, so a hold with room said yes to an item Add would throw on
        Inventory inventory = new(slots: 4);
        inventory.Add(Ore);

        Item sameIdDifferentStats = new("ore.iron", new ItemStats(EquipSlot.Weapon, StackLimit: 99));

        Assert.True(inventory.FreeSlots > 0);
        Assert.Throws<ArgumentException>(() => inventory.HasRoomFor(sameIdDifferentStats));
        Assert.Throws<ArgumentException>(() => inventory.Add(sameIdDifferentStats));
    }

    [Fact]
    public void AnItemOfAnotherId_IsUnaffectedByWhatIsAlreadyHeld()
    {
        // the mismatch check keys off the id, so an item that shares nothing with what is held is
        // still answered on room alone
        Inventory inventory = new(slots: 1);
        inventory.Add(Ore);

        Assert.False(inventory.HasRoomFor(Cell));
        Assert.True(inventory.HasRoomFor(Ore));
    }

    [Fact]
    public void AHoldWithNoSlotsAtAll_HoldsNothingAndSaysSo()
    {
        // a legitimate thing for content to author, and every question asked of it has an answer
        Inventory inventory = new(slots: 0);

        Assert.False(inventory.HasRoomFor(Ore));
        Assert.False(inventory.Add(Ore));
        Assert.Equal(0, inventory.FreeSlots);
    }
}
