namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Loadout"/>: the eight slots a ship has, the auto-slot rule a collected item
/// goes through, and what happens once they are full.
/// </summary>
public sealed class LoadoutTests
{
    static readonly Item Cannon = new("weapon.cannon", new ItemStats(EquipSlot.Weapon, StackLimit: 1));
    static readonly Item Deflector = new("shield.deflector", new ItemStats(EquipSlot.Shield, StackLimit: 1));
    static readonly Item Orb = new("orb.sentry", new ItemStats(EquipSlot.Additional, StackLimit: 1));
    static readonly Item Ore = new("ore.iron", new ItemStats(EquipSlot.None, StackLimit: 20));

    [Fact]
    public void AShip_HasFourWeaponSlotsTwoShieldSlotsAndTwoAdditionalSlots()
    {
        Assert.Equal(4, Loadout.SlotsFor(EquipSlot.Weapon));
        Assert.Equal(2, Loadout.SlotsFor(EquipSlot.Shield));
        Assert.Equal(2, Loadout.SlotsFor(EquipSlot.Additional));
    }

    [Fact]
    public void TheShieldAndAdditionalCounts_AreTheOnesTheShipAlreadyFitsStatsInto()
    {
        // written down once rather than twice, so the item slots and the stats slots cannot drift
        // apart into a ship with two shield slots that fits three shields
        Assert.Equal(Shielding.Slots, Loadout.SlotsFor(EquipSlot.Shield));
        Assert.Equal(Orbs.Slots, Loadout.SlotsFor(EquipSlot.Additional));
    }

    [Fact]
    public void ANewLoadout_IsEmpty()
    {
        // "critically low on everything" is a ship with the slots and nothing in them
        Loadout loadout = new();

        Assert.Empty(loadout.Weapons);
        Assert.Empty(loadout.Shields);
        Assert.Empty(loadout.Additional);
        Assert.Equal(0, loadout.Count);
        Assert.Equal(4, loadout.FreeIn(EquipSlot.Weapon));
    }

    [Fact]
    public void ACollectedItem_GoesStraightIntoAFreeSlotOfItsKind()
    {
        // or the player collects their first weapon and flies on unarmed, having been given no
        // reason to think a screen exists that they have to open
        Loadout loadout = new();

        Assert.True(loadout.TryEquip(Cannon));
        Assert.True(loadout.TryEquip(Deflector));
        Assert.True(loadout.TryEquip(Orb));

        Assert.Equal([Cannon], loadout.Weapons);
        Assert.Equal([Deflector], loadout.Shields);
        Assert.Equal([Orb], loadout.Additional);
        Assert.Equal(3, loadout.Count);
    }

    [Fact]
    public void AnItemOnlyEverGoesIntoASlotOfItsOwnKind()
    {
        Loadout loadout = new();

        loadout.TryEquip(Cannon);

        Assert.Empty(loadout.Shields);
        Assert.Empty(loadout.Additional);
        Assert.Equal(2, loadout.FreeIn(EquipSlot.Shield));
    }

    [Fact]
    public void SeveralOfTheSameWeapon_FillSeveralSlots()
    {
        // four weapon slots are four weapons, not four different weapons
        Loadout loadout = new();

        for (int fitted = 0; fitted < 4; fitted++)
        {
            Assert.True(loadout.TryEquip(Cannon));
        }

        Assert.Equal([Cannon, Cannon, Cannon, Cannon], loadout.Weapons);
        Assert.Equal(0, loadout.FreeIn(EquipSlot.Weapon));
    }

    [Fact]
    public void AnItemThatFitsNothing_IsNeverEquipped()
    {
        Loadout loadout = new();

        Assert.False(loadout.TryEquip(Ore));

        Assert.Equal(0, loadout.Count);
        Assert.Equal(0, Loadout.SlotsFor(EquipSlot.None));
        Assert.Empty(loadout.In(EquipSlot.None));
    }

    [Fact]
    public void OnceTheSlotsAreFull_TheItemIsSimplyCarried()
    {
        // moving it in is then the player's decision to make deliberately
        Loadout loadout = new();
        loadout.TryEquip(Deflector);
        loadout.TryEquip(Deflector);

        Assert.False(loadout.TryEquip(Deflector));

        Assert.Equal(2, loadout.Shields.Count);
    }

    [Fact]
    public void Unequip_TakesTheLastOneFittedBackOut()
    {
        // which leaves the earlier ones where the player put them
        Loadout loadout = new();
        loadout.TryEquip(Cannon);
        loadout.TryEquip(Cannon);

        Assert.True(loadout.Unequip(Cannon));

        Assert.Equal([Cannon], loadout.Weapons);
        Assert.Equal(3, loadout.FreeIn(EquipSlot.Weapon));
    }

    [Fact]
    public void UnequippingSomethingThatIsNotFitted_SaysSoRatherThanThrowing()
    {
        Loadout loadout = new();

        Assert.False(loadout.Unequip(Cannon));
        Assert.False(loadout.Unequip(Ore));
    }

    [Fact]
    public void In_ReadsTheSlotsOfAKind()
    {
        Loadout loadout = new();
        loadout.TryEquip(Orb);

        Assert.Equal([Orb], loadout.In(EquipSlot.Additional));
        Assert.Empty(loadout.In(EquipSlot.Weapon));
    }

    [Fact]
    public void ASlotNoShipHas_IsRefusedWhereItIsAskedFor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Loadout.SlotsFor((EquipSlot)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Loadout().In((EquipSlot)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Loadout().FreeIn((EquipSlot)99));
    }

    [Fact]
    public void ALoadoutCannotFitNothing()
    {
        Assert.Throws<ArgumentNullException>(() => new Loadout().TryEquip(null!));
        Assert.Throws<ArgumentNullException>(() => new Loadout().Unequip(null!));
    }
}
