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
