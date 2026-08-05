namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Shielding"/>: the two shield slots and what filling them freely adds up to —
/// two of a kind doubling one effect, a mix giving both.
/// </summary>
public sealed class ShieldingTests
{
    [Fact]
    public void NothingFitted_IsNoLayerAndNoBounce()
    {
        Assert.Empty(Shielding.None.Fitted);
        Assert.Equal(0, Shielding.None.Capacity);
        Assert.Equal(0, Shielding.None.ReflectedFraction);
    }

    [Fact]
    public void OneAbsorbingShield_IsThatShieldsLayer()
    {
        Shielding shielding = new(ShieldStats.Absorbing(40));

        Assert.Equal(40, shielding.Capacity);
        Assert.Equal(0, shielding.ReflectedFraction);
    }

    [Fact]
    public void TwoOfAKind_DoublesTheEffect()
    {
        // the doubling is not a rule written anywhere: slots add up, so filling both with the same
        // shield doubles what one of them was worth. A rule that said "double when they match" would
        // have to say what happens when they nearly match.
        Shielding absorbing = new(ShieldStats.Absorbing(40), ShieldStats.Absorbing(40));
        Shielding reflecting = new(ShieldStats.Reflecting(0.25), ShieldStats.Reflecting(0.25));

        Assert.Equal(80, absorbing.Capacity);
        Assert.Equal(0.5, reflecting.ReflectedFraction);
    }

    [Fact]
    public void AMixedPair_GivesBothEffectsAtSingleStrength()
    {
        Shielding shielding = new(ShieldStats.Absorbing(40), ShieldStats.Reflecting(0.25));

        Assert.Equal(40, shielding.Capacity);
        Assert.Equal(0.25, shielding.ReflectedFraction);
    }

    [Fact]
    public void TwoShieldsThatWouldBounceBackMoreThanArrived_BounceBackAllOfIt()
    {
        // held at the whole hit rather than refused: each shield is authored on its own and neither
        // is a mistake, and a pair that adds past one is a tuning decision showing up as total
        // immunity — not damage travelling back out larger than it came in
        Shielding shielding = new(ShieldStats.Reflecting(0.8), ShieldStats.Reflecting(0.8));

        Assert.Equal(1, shielding.ReflectedFraction);
    }

    [Fact]
    public void WhatIsFitted_IsHeldInTheOrderItWasSlotted()
    {
        ShieldStats first = ShieldStats.Absorbing(40);
        ShieldStats second = ShieldStats.Reflecting(0.25);

        Shielding shielding = new(first, second);

        Assert.Equal([first, second], shielding.Fitted);
    }

    [Fact]
    public void MoreShieldsThanThereAreSlots_IsRefusedWhereItIsFitted()
    {
        Assert.Equal(2, Shielding.Slots);
        Assert.Throws<ArgumentException>(() => new Shielding(
            ShieldStats.Absorbing(40),
            ShieldStats.Absorbing(40),
            ShieldStats.Absorbing(40)));
    }

    [Fact]
    public void AnEmptySlot_IsAnEmptySlotRatherThanAShieldThatIsNothing()
    {
        Assert.Throws<ArgumentException>(() => new Shielding(ShieldStats.Absorbing(40), null!));
        Assert.Throws<ArgumentNullException>(() => new Shielding((ShieldStats[])null!));
    }
}
