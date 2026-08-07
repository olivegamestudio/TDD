namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="ItemStats"/>: what an item has to say about itself before anything can hold or
/// fit one.
/// </summary>
public sealed class ItemStatsTests
{
    [Fact]
    public void AnItemNobodyThoughtAbout_IsCarriedOneToASlotAndFitsNothing()
    {
        Assert.Equal(EquipSlot.None, ItemStats.Single.Slot);
        Assert.Equal(1, ItemStats.Single.StackLimit);
    }

    [Fact]
    public void AnItemThatFitsNoSlot_IsTheDefaultRatherThanSomethingAuthorsOptInTo()
    {
        // so an item nobody thought about is one that is merely carried, rather than one that
        // silently arms a ship
        Assert.Equal(EquipSlot.None, default(EquipSlot));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(ItemStats.MaxStackLimit)]
    public void AnItemStacksAsHighAsItWasAuthoredTo(int limit)
    {
        Assert.Equal(limit, new ItemStats(EquipSlot.None, limit).StackLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnItemThatCanBeOwnedAndNeverHeld_IsRejected(int limit)
    {
        // a limit of zero opens a slot that immediately has no room in it, and a negative one is the
        // same defect with the sign lost
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStats(EquipSlot.None, limit));
    }

    [Fact]
    public void AStackTallerThanTheDesignAllows_IsRejected()
    {
        // carrying capacity is a designed constraint, and one item type stacking to a thousand is a
        // hole straight through it
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ItemStats(EquipSlot.None, ItemStats.MaxStackLimit + 1));
    }

    [Fact]
    public void AnItemFittingASlotNoShipHas_IsRejectedWhereItIsAuthored()
    {
        // a loadout matches an item to a slot by this value, so an item naming a slot that does not
        // exist would simply refuse to equip, for ever, looking like a full ship
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStats((EquipSlot)99, StackLimit: 1));
    }
}
