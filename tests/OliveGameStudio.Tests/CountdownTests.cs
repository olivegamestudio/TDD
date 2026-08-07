namespace OliveGameStudio.Tests;

public sealed class CountdownTests
{
    [Fact]
    public void Elapses_WhenTicksSumToTheDuration()
    {
        Countdown countdown = new(TimeSpan.FromSeconds(2));

        countdown.Tick(TimeSpan.FromSeconds(1));
        Assert.False(countdown.IsElapsed);

        countdown.Tick(TimeSpan.FromSeconds(1));
        Assert.True(countdown.IsElapsed);
    }

    [Fact]
    public void Accumulates_ManySmallTicks()
    {
        // frame-sized ticks, not one lucky boundary hit
        Countdown countdown = new(TimeSpan.FromSeconds(2));

        countdown.Tick(TimeSpan.FromSeconds(0.5));
        countdown.Tick(TimeSpan.FromSeconds(0.5));
        countdown.Tick(TimeSpan.FromSeconds(0.5));
        Assert.False(countdown.IsElapsed);      // 1.5s — not yet

        countdown.Tick(TimeSpan.FromSeconds(0.5));
        Assert.True(countdown.IsElapsed);       // 2.0s — now
    }

    [Fact]
    public void Elapses_WhenASingleTickOvershoots()
    {
        // a long frame (hitch, breakpoint) must not skip the elapse
        Countdown countdown = new(TimeSpan.FromSeconds(2));

        countdown.Tick(TimeSpan.FromSeconds(5));

        Assert.True(countdown.IsElapsed);
    }

    [Fact]
    public void Reset_Rearms()
    {
        Countdown countdown = new(TimeSpan.FromSeconds(2));
        countdown.Tick(TimeSpan.FromSeconds(2));
        Assert.True(countdown.IsElapsed);

        countdown.Reset();
        Assert.False(countdown.IsElapsed);      // back to the full duration

        countdown.Tick(TimeSpan.FromSeconds(2));
        Assert.True(countdown.IsElapsed);       // and it elapses again
    }

    [Fact]
    public void Constructor_RejectsANegativeDuration()
    {
        // A countdown built with a negative duration starts elapsed and Reset puts the same
        // negative value back, so it can never be rearmed — Reset_Rearms above is the contract it
        // would break. Refusing it at construction is the only place the mistake is still legible.
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new Countdown(TimeSpan.FromSeconds(-1)));

        Assert.Equal("duration", exception.ParamName);
    }

    [Fact]
    public void Tick_RejectsANegativeTime()
    {
        // Ticking backwards is not slowing a countdown down, it is un-elapsing one: the same
        // mistake Meter.Reduce refuses, where a calculation that came out backwards heals the
        // thing it was aimed at instead of hurting it.
        Countdown countdown = new(TimeSpan.FromSeconds(2));

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => countdown.Tick(TimeSpan.FromSeconds(-1)));

        Assert.Equal("time", exception.ParamName);
    }

    [Fact]
    public void Tick_LeavesAnElapsedCountdownElapsed()
    {
        // The point of refusing the negative tick: IsElapsed goes one way, and only Reset comes
        // back. A countdown that quietly rearms itself is a screen that plays twice.
        Countdown countdown = new(TimeSpan.FromSeconds(2));
        countdown.Tick(TimeSpan.FromSeconds(2));
        Assert.True(countdown.IsElapsed);

        Assert.Throws<ArgumentOutOfRangeException>(() => countdown.Tick(TimeSpan.FromSeconds(-1)));

        Assert.True(countdown.IsElapsed);
    }

    [Fact]
    public void Tick_AllowsAZeroTime()
    {
        // A frame that took no measurable time is a real frame, not a caller mistake — refusing it
        // would make the game loop check the clock before it was allowed to report it.
        Countdown countdown = new(TimeSpan.FromSeconds(2));

        countdown.Tick(TimeSpan.Zero);

        Assert.False(countdown.IsElapsed);
        Assert.Equal(TimeSpan.FromSeconds(2), countdown.Remaining);
    }

    [Fact]
    public void Constructor_AllowsAZeroDuration()
    {
        // Zero is not the same mistake: a countdown given no time to run is finished the moment it
        // is made and stays finished through any number of resets, which is a coherent answer and
        // is how a splash screen configured to last no time skips itself.
        Countdown countdown = new(TimeSpan.Zero);

        Assert.True(countdown.IsElapsed);

        countdown.Reset();

        Assert.True(countdown.IsElapsed);
    }
}
