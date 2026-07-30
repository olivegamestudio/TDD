namespace OliveGameStudio.Tests;

public sealed class ScaledFrameTimeControllerTests
{
    [Fact]
    public void PassesFrameTimeThrough_AtTheDefaultScale()
    {
        ScaledFrameTimeController frameTime = new();

        // an untouched controller must not alter the frame time at all
        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void HalvesFrameTime_AtHalfScale()
    {
        ScaledFrameTimeController frameTime = new() { TimeScale = 0.5 };

        Assert.Equal(TimeSpan.FromMilliseconds(8), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void StretchesFrameTime_AtDoubleScale()
    {
        ScaledFrameTimeController frameTime = new() { TimeScale = 2 };

        Assert.Equal(TimeSpan.FromMilliseconds(32), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void FreezesTime_AtZeroScale()
    {
        ScaledFrameTimeController frameTime = new() { TimeScale = 0 };

        // frames keep arriving at zero scale; none of them may advance the game
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromDays(1)));
    }

    [Fact]
    public void ResumesTime_WhenTheScaleIsRestored()
    {
        ScaledFrameTimeController frameTime = new() { TimeScale = 0 };
        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.FromMilliseconds(16)));

        frameTime.TimeScale = 1;

        Assert.Equal(TimeSpan.FromMilliseconds(16), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void RejectsANegativeScale()
    {
        // running time backwards would rewind countdowns and animations
        ScaledFrameTimeController frameTime = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => frameTime.TimeScale = -1);
        Assert.Equal(1, frameTime.TimeScale);   // and the rejected value is not kept
    }
}
