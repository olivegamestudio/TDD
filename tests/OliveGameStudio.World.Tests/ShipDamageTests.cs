namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers what a hit costs a <see cref="Ship"/>: the shield layer standing in front of the hull,
/// what a reflecting shield sends back, and what is left when both are done.
/// </summary>
public sealed class ShipDamageTests
{
    static readonly ShipHandling Handling = new(Acceleration: 100, Drag: 0.5, TurnRate: 2);

    static Ship ShipWith(Shielding? shielding = null)
    {
        Ship ship = new(new ShipProfile(Handling, Health: 80, Durability: 40, Loadout: []));

        if (shielding is not null)
        {
            ship.Fit(shielding);
        }

        return ship;
    }

    [Fact]
    public void AShipWithNoShield_TakesEveryHitOnTheHull()
    {
        Ship ship = ShipWith();

        DamageOutcome outcome = ship.TakeDamage(30);

        Assert.Equal(30, outcome.Dealt);
        Assert.Equal(0, outcome.Absorbed);
        Assert.Equal(0, outcome.Reflected);
        Assert.Equal(50, ship.Health.Current);
    }

    [Fact]
    public void FittingAnAbsorbingShield_PutsALayerInFrontOfTheHull()
    {
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        Assert.Equal(40, ship.Shield.Maximum);
        Assert.Equal(40, ship.Shield.Current);
    }

    [Fact]
    public void AHitTheShieldCanHold_NeverReachesTheHull()
    {
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        DamageOutcome outcome = ship.TakeDamage(30);

        Assert.Equal(30, outcome.Absorbed);
        Assert.Equal(0, outcome.Dealt);
        Assert.Equal(10, ship.Shield.Current);
        Assert.Equal(80, ship.Health.Current);
    }

    [Fact]
    public void AHitBiggerThanTheShield_BleedsTheRemainderIntoHealth()
    {
        // the shield depletes before health rather than instead of it: one big hit is not survived
        // by a small shield, it is reduced by one
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        DamageOutcome outcome = ship.TakeDamage(70);

        Assert.Equal(40, outcome.Absorbed);
        Assert.Equal(30, outcome.Dealt);
        Assert.True(ship.Shield.IsEmpty);
        Assert.Equal(50, ship.Health.Current);
    }

    [Fact]
    public void AReflectingShield_SendsItsShareBackAndTheShipNeverTakesIt()
    {
        // bouncing damage back means it does not land: the share that is reflected is not also paid
        // for by the hull, or "reflecting" would only be a way of hurting the attacker too
        Ship ship = ShipWith(new Shielding(ShieldStats.Reflecting(0.25)));

        DamageOutcome outcome = ship.TakeDamage(40);

        Assert.Equal(10, outcome.Reflected);
        Assert.Equal(30, outcome.Dealt);
        Assert.Equal(50, ship.Health.Current);
    }

    [Fact]
    public void AMixedPair_BouncesItsShareBackAndSoaksWhatIsLeft()
    {
        // the combo, resolved in the order the fiction implies: what is reflected never arrives, and
        // the layer takes what does
        Ship ship = ShipWith(new Shielding(ShieldStats.Reflecting(0.5), ShieldStats.Absorbing(40)));

        DamageOutcome outcome = ship.TakeDamage(100);

        Assert.Equal(50, outcome.Reflected);
        Assert.Equal(40, outcome.Absorbed);
        Assert.Equal(10, outcome.Dealt);
        Assert.Equal(70, ship.Health.Current);
    }

    [Fact]
    public void AHitIsNeverWorthMoreThanItArrivedAs()
    {
        // what is sent back, what the layer took and what the hull took are the whole hit and no more
        Ship ship = ShipWith(new Shielding(ShieldStats.Reflecting(0.25), ShieldStats.Absorbing(10)));

        DamageOutcome outcome = ship.TakeDamage(60);

        Assert.Equal(60, outcome.Reflected + outcome.Absorbed + outcome.Dealt, 10);
    }

    [Fact]
    public void ShieldsThatBounceBackEverything_LeaveNothingToSoakOrSurvive()
    {
        Ship ship = ShipWith(new Shielding(ShieldStats.Reflecting(0.8), ShieldStats.Reflecting(0.8)));

        DamageOutcome outcome = ship.TakeDamage(50);

        Assert.Equal(50, outcome.Reflected);
        Assert.Equal(0, outcome.Dealt);
        Assert.Equal(80, ship.Health.Current);
    }

    [Fact]
    public void AHitOfNothing_CostsNothing()
    {
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        DamageOutcome outcome = ship.TakeDamage(0);

        Assert.Equal(0, outcome.Reflected);
        Assert.Equal(0, outcome.Absorbed);
        Assert.Equal(0, outcome.Dealt);
        Assert.Equal(40, ship.Shield.Current);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AHitThatIsNotAHit_IsRefusedWhereItLands(double amount)
    {
        // Meter.Reduce refuses a negative for the same reason: a damage calculation that came out
        // backwards would heal the thing it was aimed at
        Ship ship = ShipWith();

        Assert.Throws<ArgumentOutOfRangeException>(() => ship.TakeDamage(amount));
    }

    public static TheoryData<Shielding, double> InfiniteHits => new()
    {
        // The two shield configurations reach a bad number by different IEEE 754 routes, so a guard
        // covering only one of them leaves the other reporting nonsense. With nothing reflecting,
        // infinity * 0 is NaN; with something reflecting, infinity - infinity is NaN.
        { Shielding.None, double.PositiveInfinity },
        { new Shielding(ShieldStats.Reflecting(0.25)), double.PositiveInfinity },
        { new Shielding(ShieldStats.Absorbing(40)), double.PositiveInfinity },
        { Shielding.None, double.NegativeInfinity },
        { new Shielding(ShieldStats.Reflecting(0.25)), double.NegativeInfinity },
    };

    [Theory]
    [MemberData(nameof(InfiniteHits))]
    public void AnInfiniteHit_IsRefusedAsTheHitItActuallyWas(Shielding shielding, double amount)
    {
        // The refusal has to name the number the caller passed. It used to name a NaN nobody passed:
        // the intermediate arithmetic above turned an infinite hit into one by either of the routes
        // named on InfiniteHits, and the NaN then surfaced from inside Meter.Reduce as "a meter
        // holds real numbers only" — which blamed the meter for the caller's number, pointed at a
        // layer the caller never called, and reported a value that appears nowhere in the call.
        Ship ship = ShipWith(shielding);

        ArgumentOutOfRangeException refusal =
            Assert.Throws<ArgumentOutOfRangeException>(() => ship.TakeDamage(amount));

        Assert.Equal(amount, refusal.ActualValue);
        Assert.Equal("amount", refusal.ParamName);
    }

    [Fact]
    public void AHitThatIsRefused_CostsTheShipNothingOnTheWayOut()
    {
        // the guard stands in front of the arithmetic rather than among it, so a refused hit is a
        // hit that never happened — a ship left half damaged by a throw is a ship whose condition
        // depends on how far down TakeDamage the bad number got
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        Assert.Throws<ArgumentOutOfRangeException>(() => ship.TakeDamage(double.PositiveInfinity));

        Assert.Equal(40, ship.Shield.Current);
        Assert.Equal(80, ship.Health.Current);
    }

    [Fact]
    public void TheBiggestHitADoubleCanHold_StillLands()
    {
        // what is refused is a number that is not an amount of damage, not a large one. The world is
        // unbounded and nothing caps what a weapon may be authored to hit for, so the boundary is
        // finiteness — the same line QuestTrigger draws around a distance.
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        DamageOutcome outcome = ship.TakeDamage(double.MaxValue);

        Assert.Equal(40, outcome.Absorbed);
        Assert.True(ship.Shield.IsEmpty);
        Assert.True(ship.Health.IsEmpty);
    }

    [Fact]
    public void ANewShip_HasNothingInItsShieldSlots()
    {
        Ship ship = ShipWith();

        Assert.Empty(ship.Shielding.Fitted);
        Assert.Equal(0, ship.Shield.Maximum);
    }

    [Fact]
    public void RefittingShields_BringsUpTheLayerTheNewShieldsAreWorth()
    {
        // the layer belongs to what is fitted, so changing what is fitted changes the layer rather
        // than leaving the ship carrying the pool the last shield gave it
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));
        ship.TakeDamage(30);

        ship.Fit(new Shielding(ShieldStats.Absorbing(100)));

        Assert.Equal(100, ship.Shield.Maximum);
        Assert.Equal(100, ship.Shield.Current);
    }

    [Fact]
    public void StrippingTheShieldSlots_LeavesTheShipOneHitFromTheHull()
    {
        Ship ship = ShipWith(new Shielding(ShieldStats.Absorbing(40)));

        ship.Fit(Shielding.None);

        Assert.Equal(0, ship.Shield.Maximum);
        Assert.Equal(30, ship.TakeDamage(30).Dealt);
    }

    [Fact]
    public void FittingNothingAtAll_IsRefused()
    {
        // Shielding.None is how a ship carries no shields; null is a caller that lost track of them.
        // The cast is what tells the two Fit overloads apart — a bare null names neither, and which
        // of the ship's slots is being emptied is exactly what this asks about.
        Ship ship = ShipWith();

        Assert.Throws<ArgumentNullException>(() => ship.Fit((Shielding)null!));
    }

    [Fact]
    public void TwoShipsWearingTheSameShields_WearThemSeparately()
    {
        Shielding shielding = new(ShieldStats.Absorbing(40));
        Ship first = ShipWith(shielding);
        Ship second = ShipWith(shielding);

        first.TakeDamage(30);

        Assert.Equal(10, first.Shield.Current);
        Assert.Equal(40, second.Shield.Current);
    }
}
