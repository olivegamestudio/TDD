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
}
