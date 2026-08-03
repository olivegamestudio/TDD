namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Item"/> and <see cref="Inventory"/>: what an item has to have to exist, and
/// what holding a set of them promises.
/// </summary>
public sealed class InventoryTests
{
    static readonly Item Scrap = new("scrap");
    static readonly Item Cell = new("power-cell");

    [Fact]
    public void AnItem_IsKnownByItsIdentifier()
    {
        Assert.Equal("scrap", Scrap.Id);
    }

    [Fact]
    public void TwoItemsWithTheSameIdentifier_AreTheSameItem()
    {
        // a record by value, so an item read back from a save is the item the game shipped rather
        // than something that merely looks like it
        Assert.Equal(new Item("scrap"), Scrap);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnItemNothingCanName_IsRejected(string? id)
    {
        // an item with no identifier cannot be equipped, found in a save, or told apart from
        // another one, so it fails where it is built rather than wherever it is first looked for
        Assert.ThrowsAny<ArgumentException>(() => new Item(id!));
    }

    [Fact]
    public void ANewInventory_IsEmpty()
    {
        Inventory inventory = new();

        Assert.Empty(inventory.Items);
        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void AnInventory_HoldsWhatItWasStockedWith()
    {
        Inventory inventory = new([Scrap, Cell]);

        Assert.Equal([Scrap, Cell], inventory.Items);
    }

    [Fact]
    public void AnInventory_KeepsTheOrderThingsArrivedIn()
    {
        // what the player picked up, rather than an ordering nothing chose
        Inventory inventory = new([Cell]);

        inventory.Add(Scrap);

        Assert.Equal([Cell, Scrap], inventory.Items);
    }

    [Fact]
    public void StockingAnInventory_CopiesTheContentItWasGiven()
    {
        // a template is authored content and never changes, so handing over a starting loadout must
        // not hand over a list the game then plays with
        List<Item> authored = [Scrap];
        Inventory inventory = new(authored);

        inventory.Add(Cell);

        Assert.Equal([Scrap], authored);
    }

    [Fact]
    public void Remove_GivesUpOneOfTheItem()
    {
        Inventory inventory = new([Scrap, Scrap]);

        Assert.True(inventory.Remove(Scrap));

        Assert.Equal([Scrap], inventory.Items);
    }

    [Fact]
    public void RemovingSomethingThatIsNotHeld_SaysSoRatherThanThrowing()
    {
        Inventory inventory = new();

        Assert.False(inventory.Remove(Scrap));
    }

    [Fact]
    public void Contains_FindsAnItemByValue()
    {
        Inventory inventory = new([Scrap]);

        Assert.True(inventory.Contains(new Item("scrap")));
        Assert.False(inventory.Contains(Cell));
    }

    [Fact]
    public void AnInventoryCannotHoldNothing()
    {
        Assert.Throws<ArgumentNullException>(() => new Inventory(null!));
        Assert.Throws<ArgumentException>(() => new Inventory([null!]));
        Assert.Throws<ArgumentNullException>(() => new Inventory().Add(null!));
    }
}
