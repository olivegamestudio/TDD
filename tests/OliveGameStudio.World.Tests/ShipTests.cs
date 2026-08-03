namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Ship"/>: that a hull is built from what content authored, that the ship
/// flown and the ship owned cannot disagree, and that two ships off the same profile are two ships.
/// </summary>
public sealed class ShipTests
{
    static readonly ShipHandling Handling = new(Acceleration: 100, Drag: 0.5, TurnRate: 2);

    static ShipProfile ProfileWith(IReadOnlyList<Item>? loadout = null) =>
        new(Handling, Health: 80, Durability: 40, loadout ?? []);

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
    public void AShip_IsFittedWithWhatItsProfileCameWith()
    {
        Item cell = new("power-cell");

        Ship ship = new(ProfileWith([cell]));

        Assert.Equal([cell], ship.Loadout.Items);
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
            Loadout: []);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(unflyable));
    }

    [Fact]
    public void AHullWithAPoolNothingCouldEmpty_IsRejectedWhereItIsBuilt()
    {
        ShipProfile indestructible = new(
            Handling,
            Health: double.PositiveInfinity,
            Durability: 40,
            Loadout: []);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(indestructible));
    }
}
