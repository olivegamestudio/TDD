namespace OliveGameStudio.Tests;

public sealed class PassThroughFrameTimeControllerTests
{
    [Fact]
    public void ReturnsTheFrameTimeUnchanged()
    {
        PassThroughFrameTimeController frameTime = new();

        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.Zero));
    }

    [Fact]
    public void DoesNotAbsorbLongFrames()
    {
        // the shipping controller has no opinion on stalls; a long frame passes through intact
        PassThroughFrameTimeController frameTime = new();

        Assert.Equal(TimeSpan.FromSeconds(5), frameTime.Filter(TimeSpan.FromSeconds(5)));
    }
}
