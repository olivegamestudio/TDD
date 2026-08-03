namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers <see cref="Meter"/>: that a pool starts full, cannot be pushed past either end, and
/// refuses a maximum nothing could ever empty or fill.
/// </summary>
public sealed class MeterTests
{
    [Fact]
    public void ANewMeter_IsFull()
    {
        Meter meter = new(100);

        Assert.Equal(100, meter.Maximum);
        Assert.Equal(100, meter.Current);
        Assert.False(meter.IsEmpty);
    }

    [Fact]
    public void AMeterOfNothing_IsEmptyFromTheStart()
    {
        // a ship with no shield fitted, rather than a ship whose shield is down
        Meter meter = new(0);

        Assert.Equal(0, meter.Maximum);
        Assert.True(meter.IsEmpty);
    }

    [Fact]
    public void Reduce_TakesFromWhatIsLeft()
    {
        Meter meter = new(100);

        meter.Reduce(30);

        Assert.Equal(70, meter.Current);
        Assert.Equal(100, meter.Maximum);
    }

    [Fact]
    public void Reduce_StopsAtEmpty()
    {
        // more damage than there was health left is not negative health; it is destroyed
        Meter meter = new(100);

        meter.Reduce(250);

        Assert.Equal(0, meter.Current);
        Assert.True(meter.IsEmpty);
    }

    [Fact]
    public void Restore_StopsAtFull()
    {
        Meter meter = new(100);
        meter.Reduce(90);

        meter.Restore(500);

        Assert.Equal(100, meter.Current);
    }

    [Fact]
    public void AnEmptyMeter_CanBeFilledAgain()
    {
        // emptying is not a one way door here. What being empty *means* belongs to whatever owns
        // the meter, and a repaired hull and a destroyed one are that owner's distinction to make.
        Meter meter = new(100);
        meter.Reduce(100);

        meter.Restore(40);

        Assert.Equal(40, meter.Current);
        Assert.False(meter.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ChangingByNothing_ChangesNothing(double maximum)
    {
        Meter meter = new(maximum);

        meter.Reduce(0);
        meter.Restore(0);

        Assert.Equal(maximum, meter.Current);
    }

    [Fact]
    public void ANegativeMaximum_IsRejected()
    {
        // a pool that is empty before anything touches it is a content mistake, and one that would
        // only ever show up as a ship that arrives destroyed
        Assert.Throws<ArgumentOutOfRangeException>(() => new Meter(-1));
    }

    [Fact]
    public void AMaximumThatIsNotANumber_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Meter(double.NaN));
    }

    [Fact]
    public void AnInfiniteMaximum_IsRejected()
    {
        // the mirror of the negative case: no amount of damage can empty it, so whatever the pool
        // was protecting can never be destroyed and nothing anywhere would report it
        Assert.Throws<ArgumentOutOfRangeException>(() => new Meter(double.PositiveInfinity));
    }

    [Fact]
    public void ReducingByANegativeAmount_IsRejected()
    {
        // reading it as a restore would let a damage calculation that came out backwards heal the
        // thing it was aimed at
        Meter meter = new(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => meter.Reduce(-1));
        Assert.Equal(100, meter.Current);
    }

    [Fact]
    public void RestoringByANegativeAmount_IsRejected()
    {
        Meter meter = new(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => meter.Restore(-1));
        Assert.Equal(100, meter.Current);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ReducingByAnAmountThatIsNotAFiniteNumber_LeavesTheMeterReadable(double amount)
    {
        // NaN is refused outright; an infinite reduction is simply everything there was. What both
        // must not do is leave Current holding a number nothing downstream can compare against.
        Meter meter = new(100);

        try
        {
            meter.Reduce(amount);
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        Assert.True(double.IsFinite(meter.Current), "the meter stopped holding a real number");
        Assert.InRange(meter.Current, 0, 100);
    }
}
