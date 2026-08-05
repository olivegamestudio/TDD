namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="OrbStats"/>: that an orb is authored as one of the two things an orb does, and
/// that the numbers a companion object cannot run on are refused where they are written.
/// </summary>
public sealed class OrbStatsTests
{
    [Fact]
    public void AnOrbitingOrb_CirclesTheShipAtTheRadiusAndRateItWasAuthoredWith()
    {
        OrbStats orb = OrbStats.Orbiting(radius: 30, angularSpeed: 2);

        Assert.Equal(OrbBehaviour.Orbiting, orb.Behaviour);
        Assert.Equal(30, orb.Radius);
        Assert.Equal(2, orb.AngularSpeed);
    }

    [Fact]
    public void ATrackingOrb_HoldsItsStationRatherThanCirclingIt()
    {
        // An orbit rate of zero is what "does not circle" is expressed as, so a caller placing the
        // slots adds up one formula rather than asking each orb which kind it is — the same reason
        // a reflecting shield reports a capacity of zero rather than refusing to be asked.
        OrbStats orb = OrbStats.Tracking(radius: 30);

        Assert.Equal(OrbBehaviour.Tracking, orb.Behaviour);
        Assert.Equal(30, orb.Radius);
        Assert.Equal(0, orb.AngularSpeed);
    }

    [Fact]
    public void AnOrbThatCirclesTheOtherWay_IsAnOrbAndNotAMistake()
    {
        // the sign is which way round it goes, and both ways are ordinary content
        Assert.Equal(-2, OrbStats.Orbiting(radius: 30, angularSpeed: -2).AngularSpeed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnOrbWithNoRadius_IsRefusedWhereItIsAuthored(double radius)
    {
        // A companion object sitting on top of the ship is one the player never sees, and one an
        // endless distance away is one that is never near it. Both are content mistakes, and both
        // are cheaper to find here than in a fight.
        Assert.Throws<ArgumentOutOfRangeException>(() => OrbStats.Orbiting(radius, angularSpeed: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrbStats.Tracking(radius));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void AnOrbitingOrbThatNeverGoesRound_IsRefusedWhereItIsAuthored(double angularSpeed)
    {
        // Zero is a fitted orbiting defence that does not orbit — a slot the player filled for no
        // benefit, which is the same answer a shield with no capacity gets. An endless or unreadable
        // rate is worse: it places the orb nowhere, in silence, on every frame.
        Assert.Throws<ArgumentOutOfRangeException>(() => OrbStats.Orbiting(radius: 30, angularSpeed));
    }

    [Fact]
    public void TwoOrbsAuthoredWithTheSameNumbers_AreTheSameOrb()
    {
        // authored content, so it is a record: nothing here changes as it is played
        Assert.Equal(OrbStats.Orbiting(radius: 30, angularSpeed: 2), OrbStats.Orbiting(radius: 30, angularSpeed: 2));
        Assert.NotEqual(OrbStats.Orbiting(radius: 30, angularSpeed: 2), OrbStats.Tracking(radius: 30));
    }
}
