namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Orbs"/>: the two additional slots, that filling them freely adds nothing up,
/// and where the companion objects in them are without anybody flying them.
/// </summary>
public sealed class OrbsTests
{
    static readonly Position Ship = new(100, 200);

    [Fact]
    public void NothingFitted_IsNoCompanionsAtAll()
    {
        Assert.Empty(Orbs.None.Fitted);
        Assert.Empty(Orbs.None.PlaceAround(Ship, secondsFlown: 3));
    }

    [Fact]
    public void WhatIsFitted_IsHeldInTheOrderItWasSlotted()
    {
        OrbStats first = OrbStats.Orbiting(radius: 30, angularSpeed: 2);
        OrbStats second = OrbStats.Tracking(radius: 45);

        Orbs orbs = new(first, second);

        Assert.Equal([first, second], orbs.Fitted);
    }

    [Fact]
    public void MoreOrbsThanThereAreSlots_IsRefusedWhereItIsFitted()
    {
        Assert.Equal(2, Orbs.Slots);
        Assert.Throws<ArgumentException>(() => new Orbs(
            OrbStats.Tracking(radius: 30),
            OrbStats.Tracking(radius: 30),
            OrbStats.Tracking(radius: 30)));
    }

    [Fact]
    public void AnEmptySlot_IsAnEmptySlotRatherThanAnOrbThatIsNothing()
    {
        Assert.Throws<ArgumentException>(() => new Orbs(OrbStats.Tracking(radius: 30), null!));
        Assert.Throws<ArgumentNullException>(() => new Orbs((OrbStats[])null!));
    }

    [Fact]
    public void ChangingTheArrayAnOrbWasFittedFrom_DoesNotChangeWhatTheShipIsCarrying()
    {
        OrbStats[] fitted = [OrbStats.Tracking(radius: 30)];
        Orbs orbs = new(fitted);

        fitted[0] = OrbStats.Orbiting(radius: 999, angularSpeed: 9);

        Assert.Equal(OrbBehaviour.Tracking, Assert.Single(orbs.Fitted).Behaviour);
    }

    [Fact]
    public void AnOrbAtTheStartOfItsOrbit_IsDirectlyAheadOfTheShip()
    {
        // A slot's bearing is measured the way a ship's heading is: zero along the positive world Y
        // axis, increasing to starboard. It is the same convention ShipPose asks of both sides of
        // the ship, so nothing has to convert to place an orb beside one.
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        Position orb = Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 0));

        Assert.Equal(Ship.X, orb.X, 10);
        Assert.Equal(Ship.Y + 30, orb.Y, 10);
    }

    [Fact]
    public void AQuarterOfAnOrbitLater_TheOrbIsOffTheShipsStarboardSide()
    {
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: Math.PI / 2));

        Position orb = Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 1));

        Assert.Equal(Ship.X + 30, orb.X, 10);
        Assert.Equal(Ship.Y, orb.Y, 10);
    }

    [Fact]
    public void AnOrbCirclingTheOtherWay_GoesToPortInsteadOfStarboard()
    {
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: -Math.PI / 2));

        Position orb = Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 1));

        Assert.Equal(Ship.X - 30, orb.X, 10);
        Assert.Equal(Ship.Y, orb.Y, 10);
    }

    [Fact]
    public void AnOrbAlwaysCirclesTheShipRatherThanSomewhereItUsedToBe()
    {
        // the ring is measured from wherever the ship is now, so the companions come with it
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        Position orb = Assert.Single(orbs.PlaceAround(new Position(-500, 900), secondsFlown: 0));

        Assert.Equal(-500, orb.X, 10);
        Assert.Equal(930, orb.Y, 10);
    }

    [Fact]
    public void TwoOrbs_TakeAnEvenShareOfTheRingRatherThanSittingOnTopOfEachOther()
    {
        // Slots are filled freely, so two of a kind is the ordinary case rather than the exotic one.
        // Without a share of the ring each they would occupy the same point at every instant, and a
        // player who fitted two would see one.
        Orbs orbs = new(
            OrbStats.Orbiting(radius: 30, angularSpeed: 2),
            OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        IReadOnlyList<Position> placed = orbs.PlaceAround(Ship, secondsFlown: 1.7);

        Assert.Equal(2, placed.Count);
        Assert.Equal(Ship.X - (placed[0].X - Ship.X), placed[1].X, 10);
        Assert.Equal(Ship.Y - (placed[0].Y - Ship.Y), placed[1].Y, 10);
    }

    [Fact]
    public void ATrackingOrb_HoldsItsStationForAsLongAsThereIsNothingToTrack()
    {
        // Where a tracker goes is a question about enemies, and there are none — so it keeps its
        // share of the ring rather than being placed somewhere the model had to invent.
        Orbs orbs = new(OrbStats.Tracking(radius: 45));

        Position early = Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 0));
        Position later = Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 90));

        Assert.Equal(early, later);
    }

    [Fact]
    public void OrbsRunOnTheirOwnClock_SoTheSameMomentIsAlwaysTheSamePlace()
    {
        // "No manual fire input — they run themselves" is this: an orb's place is a function of what
        // it was authored with and how long has been flown, and of nothing the player pressed. It
        // also means a frame drawn twice draws it in the same place both times.
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        Assert.Equal(orbs.PlaceAround(Ship, secondsFlown: 4.5), orbs.PlaceAround(Ship, secondsFlown: 4.5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ATimeAnOrbCannotBePlacedAt_IsRefusedRatherThanDrawnNowhere(double secondsFlown)
    {
        // The sine of a time that is not a number is not a number, so every orb would be placed
        // nowhere, in full, raising nothing — the same failure the camera refuses a target for.
        // Time before the game began is not a shorter answer to where an orb is; it is a caller
        // that subtracted the wrong way round.
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        Assert.Throws<ArgumentOutOfRangeException>(() => orbs.PlaceAround(Ship, secondsFlown));
    }

    [Fact]
    public void NoTimeAtAll_IsTheStartOfTheRingRatherThanARefusal()
    {
        Orbs orbs = new(OrbStats.Orbiting(radius: 30, angularSpeed: 2));

        Assert.Single(orbs.PlaceAround(Ship, secondsFlown: 0));
    }
}
