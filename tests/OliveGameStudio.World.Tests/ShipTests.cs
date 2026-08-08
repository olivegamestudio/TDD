namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Ship"/>: that a hull is built from what content authored, that the ship
/// flown and the ship owned cannot disagree, and that two ships off the same profile are two ships.
/// </summary>
public sealed class ShipTests
{
    static readonly ShipHandling Handling = new(Acceleration: 100, Drag: 0.5, TurnRate: 2);

    static ShipProfile ProfileWith(IReadOnlyList<Item>? loadout = null) =>
        new(Handling, Health: 80, Durability: 40, CargoSlots: 16, loadout ?? [], HullRadius: 1);

    [Fact]
    public void AShip_FliesOnTheHandlingItsProfileAuthored()
    {
        Ship ship = new(ProfileWith());

        Assert.Same(Handling, ship.Handling);
        Assert.Same(ProfileWith().Handling, ship.Profile.Handling);
    }

    [Fact]
    public void AShip_CarriesThePhysicsThatFliesIt()
    {
        // The whole reason the ship owns the movement rather than sitting next to it: there is no
        // way to hand the physics one set of numbers and the ship another. Registered side by side
        // they could be given two, and the ship the player owns would not be the ship they fly.
        Ship ship = new(ProfileWith());

        ship.Movement.Update(new Player(), new ShipControls(thrust: 1, turn: 0), TimeSpan.FromSeconds(1));

        Assert.InRange(ship.Movement.Velocity.Y, 1, ship.Handling.MaximumSpeed);
    }

    [Fact]
    public void ANewShip_IsAtRestFacingForward()
    {
        // what makes a resumed game begin stationary without anybody remembering to reset anything
        Ship ship = new(ProfileWith());

        Assert.Equal(Velocity.Stationary, ship.Movement.Velocity);
        Assert.Equal(0, ship.Movement.Heading);
    }

    [Fact]
    public void ANewShip_IsUndamagedAndUnworn()
    {
        Ship ship = new(ProfileWith());

        Assert.Equal(80, ship.Health.Maximum);
        Assert.Equal(80, ship.Health.Current);
        Assert.Equal(40, ship.Durability.Maximum);
        Assert.Equal(40, ship.Durability.Current);
    }

    [Fact]
    public void AShipWithNothingFitted_HasNoShieldAtAll()
    {
        // the shield comes from what is fitted, and nothing fitted supplies one yet. A maximum of
        // zero says "no shield", which is not the same claim as "the shield is down" — but both
        // leave the ship one hit from the hull, which is why they read alike here.
        Ship ship = new(ProfileWith());

        Assert.Equal(0, ship.Shield.Maximum);
        Assert.True(ship.Shield.IsEmpty);
    }

    [Fact]
    public void FittingTwoShieldsTooLargeToAddUp_LeavesAShipShieldedRatherThanCrashingIt()
    {
        // the crash the layers between them used to allow: each shield passed ShieldStats' own
        // validation, the sum ran off the end of a double, and Meter refused the infinity that came
        // back — so a ship blew up on being handed two shields that were individually fine, and blew
        // up in Meter's words rather than anywhere the caller had been.
        Ship ship = new(ProfileWith());

        ship.Fit(new Shielding(
            ShieldStats.Absorbing(double.MaxValue),
            ShieldStats.Absorbing(double.MaxValue)));

        Assert.Equal(double.MaxValue, ship.Shield.Maximum);
        Assert.False(ship.Shield.IsEmpty);
    }

    [Fact]
    public void AShipWithNothingFitted_HasEmptyOrbSlots()
    {
        // what the design asks for at the start of the story: the additional slots come empty, and
        // no content authors an orb to put in one yet
        Ship ship = new(ProfileWith());

        Assert.Empty(ship.Orbs.Fitted);
    }

    [Fact]
    public void FittingOrbs_PutsThemInTheSlotsAndTakesOutWhatWasThere()
    {
        Ship ship = new(ProfileWith());
        OrbStats replacement = OrbStats.Tracking(radius: 45);

        ship.Fit(new Orbs(OrbStats.Orbiting(radius: 30, angularSpeed: 2)));
        ship.Fit(new Orbs(replacement));

        Assert.Equal([replacement], ship.Orbs.Fitted);
    }

    [Fact]
    public void StrippingTheOrbSlots_LeavesTheShipWithNoCompanionsAtAll()
    {
        Ship ship = new(ProfileWith());
        ship.Fit(new Orbs(OrbStats.Orbiting(radius: 30, angularSpeed: 2)));

        ship.Fit(Orbs.None);

        Assert.Empty(ship.Orbs.Fitted);
    }

    [Fact]
    public void FittingNoOrbsAtAll_IsRejectedRatherThanReadAsStrippingTheSlots()
    {
        // Orbs.None says "empty slots"; a null says the caller lost the orbs on the way here
        Ship ship = new(ProfileWith());

        Assert.Throws<ArgumentNullException>(() => ship.Fit((Orbs)null!));
    }

    [Fact]
    public void AShip_IsFittedWithWhatItsProfileCameWith()
    {
        Item cannon = new("weapon.cannon", new ItemStats(EquipSlot.Weapon, StackLimit: 1));

        Ship ship = new(ProfileWith([cannon]));

        Assert.Equal([cannon], ship.Loadout.Weapons);
    }

    [Fact]
    public void ANewHull_ComesOutOfTheYardWithEverySlotEmpty()
    {
        Ship ship = new(ProfileWith());

        Assert.Equal(0, ship.Loadout.Count);
        Assert.Equal(4, ship.Loadout.FreeIn(EquipSlot.Weapon));
        Assert.Equal(2, ship.Loadout.FreeIn(EquipSlot.Shield));
        Assert.Equal(2, ship.Loadout.FreeIn(EquipSlot.Additional));
    }

    [Fact]
    public void AHullFittedWithMoreThanItsSlotsHold_IsRejectedWhereItIsBuilt()
    {
        // fitting what fits and dropping the rest would give the player a ship quietly short of what
        // it was written to be, and there is nowhere to put the overflow before the character exists
        Item deflector = new("shield.deflector", new ItemStats(EquipSlot.Shield, StackLimit: 1));

        Assert.Throws<ArgumentException>(
            () => new Ship(ProfileWith([deflector, deflector, deflector])));
    }

    [Fact]
    public void AHullFittedWithSomethingThatFitsNoSlot_IsRejectedWhereItIsBuilt()
    {
        Item ore = new("ore.iron", new ItemStats(EquipSlot.None, StackLimit: 20));

        Assert.Throws<ArgumentException>(() => new Ship(ProfileWith([ore])));
    }

    [Fact]
    public void AHullWithAHoldOfNegativeSize_IsRejectedWhereItIsAuthored()
    {
        // every question asked of it — "is there room?", "how much is left?" — would come back a
        // nonsense the player is shown
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipProfile(Handling, Health: 80, Durability: 40, CargoSlots: -1, Loadout: [], HullRadius: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AHullWithASizeItCouldNotCollideAt_IsRejectedWhereItIsAuthored(double hullRadius)
    {
        // ShipMovement already refuses these, but only once a Ship is built — far enough from the
        // profile that the exception names hullRadius rather than the content that carried it.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipProfile(Handling, Health: 80, Durability: 40, CargoSlots: 16, Loadout: [], HullRadius: hullRadius));
    }

    [Fact]
    public void AHullRejectedForItsSize_NamesTheProfilesOwnParameter()
    {
        // the point of validating here rather than leaving it to ShipMovement: whoever authored the
        // profile is told which of the profile's numbers was wrong
        ArgumentOutOfRangeException rejected = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ShipProfile(Handling, Health: 80, Durability: 40, CargoSlots: 16, Loadout: [], HullRadius: 0));

        Assert.Equal(nameof(ShipProfile.HullRadius), rejected.ParamName);
    }

    [Fact]
    public void TwoShipsOffTheSameProfile_AreTwoShips()
    {
        // a profile is authored and shared; damage is not. Two characters flying the same hull are
        // not sharing a health pool.
        ShipProfile profile = ProfileWith();
        Ship first = new(profile);
        Ship second = new(profile);

        first.Health.Reduce(50);

        Assert.Equal(30, first.Health.Current);
        Assert.Equal(80, second.Health.Current);
        Assert.NotSame(first.Movement, second.Movement);
    }

    [Fact]
    public void AShipWithNoProfile_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Ship(null!));
    }

    [Fact]
    public void AHullThatCouldNotBeFlown_IsRejectedWhereItIsBuilt()
    {
        // ShipMovement already refuses handling that cannot be flown; building the physics with the
        // ship is what brings that refusal forward to where the content is read
        ShipProfile unflyable = new(
            new ShipHandling(Acceleration: 0, Drag: 0.5, TurnRate: 2),
            Health: 80,
            Durability: 40,
            CargoSlots: 16,
            Loadout: [],
            HullRadius: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(unflyable));
    }

    [Fact]
    public void AHullWithAPoolNothingCouldEmpty_IsRejectedWhereItIsBuilt()
    {
        ShipProfile indestructible = new(
            Handling,
            Health: double.PositiveInfinity,
            Durability: 40,
            CargoSlots: 16,
            Loadout: [],
            HullRadius: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(indestructible));
    }
}
