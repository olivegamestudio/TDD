namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Item"/> and <see cref="Inventory"/>: what an item has to have to exist, and
/// what a bounded set of slots holding them promises.
/// </summary>
public sealed class InventoryTests
{
    static readonly Item Scrap = new("scrap", new ItemStats(EquipSlot.None, StackLimit: 20));
    static readonly Item Cell = new("power-cell", ItemStats.Single);

    [Fact]
    public void AnItem_IsKnownByItsIdentifier()
    {
        Assert.Equal("scrap", Scrap.Id);
    }

    [Fact]
    public void TwoItemsWithTheSameIdentifier_AreTheSameItem()
    {
        // an item read back from a save is the item the game shipped rather than something that
        // merely looks like it — which is what lets a save carry ids and nothing else
        Assert.Equal(new Item("scrap", new ItemStats(EquipSlot.None, StackLimit: 20)), Scrap);
    }

    [Fact]
    public void AnItemIsItsIdentifier_AndNotTheNumbersItWasAuthoredWith()
    {
        // identity has to survive a stats change, or a save carrying ids could not rebuild what it
        // saved without also having stored the numbers it was saved with
        Assert.Equal(new Item("scrap", ItemStats.Single), Scrap);
        Assert.Equal(new Item("scrap", ItemStats.Single).GetHashCode(), Scrap.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnItemNothingCanName_IsRejected(string? id)
    {
        // an item with no identifier cannot be equipped, found in a save, or told apart from
        // another one, so it fails where it is built rather than wherever it is first looked for
        Assert.ThrowsAny<ArgumentException>(() => new Item(id!, ItemStats.Single));
    }

    [Fact]
    public void AnItemNobodySaidHowToHold_IsRejected()
    {
        // every holder of an item asks how high it stacks, so an item without an answer is one that
        // cannot be picked up
        Assert.Throws<ArgumentNullException>(() => new Item("scrap", null!));
    }

    [Fact]
    public void ANewInventory_IsEmptyAndAllOfItsSlotsAreFree()
    {
        Inventory inventory = new(slots: 4);

        Assert.Empty(inventory.Items);
        Assert.Equal(0, inventory.Count);
        Assert.Equal(4, inventory.Slots);
        Assert.Equal(0, inventory.UsedSlots);
        Assert.Equal(4, inventory.FreeSlots);
    }

    [Fact]
    public void AnInventory_HoldsWhatItWasStockedWith()
    {
        Inventory inventory = new(4, [Scrap, Cell]);

        Assert.Equal([Scrap, Cell], inventory.Items);
    }

    [Fact]
    public void AnInventory_KeepsTheOrderThingsArrivedIn()
    {
        // what the player picked up, rather than an ordering nothing chose
        Inventory inventory = new(4, [Cell]);

        inventory.Add(Scrap);

        Assert.Equal([Cell, Scrap], inventory.Items);
    }

    [Fact]
    public void StockingAnInventory_CopiesTheContentItWasGiven()
    {
        // a template is authored content and never changes, so handing over a starting inventory
        // must not hand over a list the game then plays with
        List<Item> authored = [Scrap];
        Inventory inventory = new(4, authored);

        inventory.Add(Cell);

        Assert.Equal([Scrap], authored);
    }

    [Fact]
    public void StockingAnInventoryWithMoreThanItHolds_IsRejectedWhereItIsAuthored()
    {
        // silently dropping the overflow would start the player short of what they were written to
        // own, and there is nowhere else to put it before the game exists
        Assert.Throws<ArgumentException>(() => new Inventory(1, [Cell, new Item("flare", ItemStats.Single)]));
    }

    [Fact]
    public void AHoldWithNegativeRoom_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(slots: -1));
    }

    [Fact]
    public void Remove_GivesUpOneOfTheItem()
    {
        Inventory inventory = new(4, [Cell, Cell]);

        Assert.True(inventory.Remove(Cell));

        Assert.Equal([Cell], inventory.Items);
    }

    [Fact]
    public void RemovingSomethingThatIsNotHeld_SaysSoRatherThanThrowing()
    {
        Inventory inventory = new(slots: 4);

        Assert.False(inventory.Remove(Scrap));
    }

    [Fact]
    public void Contains_FindsAnItemByValue()
    {
        Inventory inventory = new(4, [Scrap]);

        Assert.True(inventory.Contains(new Item("scrap", ItemStats.Single)));
        Assert.False(inventory.Contains(Cell));
    }

    [Fact]
    public void AnInventoryCannotHoldNothing()
    {
        Assert.Throws<ArgumentNullException>(() => new Inventory(4, null!));
        Assert.Throws<ArgumentException>(() => new Inventory(4, [null!]));
        Assert.Throws<ArgumentNullException>(() => new Inventory(4).Add(null!));
        Assert.Throws<ArgumentNullException>(() => new Inventory(4).Remove(null!));
        Assert.Throws<ArgumentNullException>(() => new Inventory(4).Contains(null!));
        Assert.Throws<ArgumentNullException>(() => new Inventory(4).CountOf(null!));
        Assert.Throws<ArgumentNullException>(() => new Inventory(4).HasRoomFor(null!));
    }
}
