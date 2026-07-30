namespace OliveGameStudio.Tests;

public sealed class PausableFrameTimeControllerTests
{
    [Fact]
    public void RunsAtRealTime_ByDefault()
    {
        PausableFrameTimeController frameTime = new();

        Assert.False(frameTime.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void FreezesTime_WhilePaused()
    {
        PausableFrameTimeController frameTime = new();

        frameTime.Pause();

        // frames keep arriving while paused; none of them may advance the game
        Assert.True(frameTime.IsPaused);
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromDays(1)));
    }

    [Fact]
    public void ReturnsToRealTime_WhenResumed()
    {
        PausableFrameTimeController frameTime = new();
        frameTime.Pause();
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));

        frameTime.Resume();

        Assert.False(frameTime.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void PausingTwice_LeavesItPaused()
    {
        // a repeated input must not toggle by accident
        PausableFrameTimeController frameTime = new();

        frameTime.Pause();
        frameTime.Pause();

        Assert.True(frameTime.IsPaused);
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void Toggle_FlipsBetweenPausedAndRunning()
    {
        PausableFrameTimeController frameTime = new();

        frameTime.Toggle();
        Assert.True(frameTime.IsPaused);
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));

        frameTime.Toggle();
        Assert.False(frameTime.IsPaused);
        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }
}
