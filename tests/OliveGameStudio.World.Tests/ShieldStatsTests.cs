namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="ShieldStats"/>: that a shield is one type doing one thing, and that a shield
/// which would do nothing cannot be authored.
/// </summary>
public sealed class ShieldStatsTests
{
    [Fact]
    public void AnAbsorbingShield_ContributesALayerAndBouncesNothingBack()
    {
        ShieldStats stats = ShieldStats.Absorbing(capacity: 40);

        Assert.Equal(ShieldType.Absorption, stats.Type);
        Assert.Equal(40, stats.Capacity);
        Assert.Equal(0, stats.ReflectedFraction);
    }

    [Fact]
    public void AReflectingShield_BouncesDamageBackAndContributesNoLayer()
    {
        // the two effects are not two settings of one number: a reflecting shield is not a small
        // absorption shield, it is a shield that never takes the hit in the first place
        ShieldStats stats = ShieldStats.Reflecting(fraction: 0.25);

        Assert.Equal(ShieldType.Reflection, stats.Type);
        Assert.Equal(0, stats.Capacity);
        Assert.Equal(0.25, stats.ReflectedFraction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnAbsorbingShieldThatWouldSoakNothingOrEverything_IsRefusedWhereItIsAuthored(double capacity)
    {
        // a shield contributing no layer is a content mistake that reads at play time as a fitted
        // shield doing nothing, and one contributing an endless layer is a ship nothing can hurt
        Assert.Throws<ArgumentOutOfRangeException>(() => ShieldStats.Absorbing(capacity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AReflectingShieldThatBouncesNothingOrMoreThanArrived_IsRefusedWhereItIsAuthored(double fraction)
    {
        // a share above one is not a stronger shield, it is damage invented on the way back out
        Assert.Throws<ArgumentOutOfRangeException>(() => ShieldStats.Reflecting(fraction));
    }

    [Fact]
    public void AShieldThatBouncesBackEverything_IsAllowed()
    {
        // the boundary is reachable on purpose: it is what two half-strength reflecting shields add
        // up to, and refusing it here would make the pair unauthorable one shield at a time
        ShieldStats stats = ShieldStats.Reflecting(fraction: 1);

        Assert.Equal(1, stats.ReflectedFraction);
    }

    [Fact]
    public void TwoShieldsWithTheSameNumbers_AreTheSameShield()
    {
        // stats are authored content, compared by what they say rather than by which object said it
        Assert.Equal(ShieldStats.Absorbing(40), ShieldStats.Absorbing(40));
        Assert.NotEqual(ShieldStats.Absorbing(40), ShieldStats.Absorbing(50));
    }
}
