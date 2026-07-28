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
}
