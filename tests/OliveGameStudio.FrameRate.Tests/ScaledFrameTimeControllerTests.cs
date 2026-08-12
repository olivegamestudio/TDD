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

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void RejectsAScaleThatIsNotAFiniteNumber(double scale)
    {
        // none of these multiplies a frame time into a TimeSpan the frame loop can advance by
        ScaledFrameTimeController frameTime = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => frameTime.TimeScale = scale);
        Assert.Equal(1, frameTime.TimeScale);   // and the rejected value is not kept
    }

    [Fact]
    public void StretchesFrameTime_AtAScaleTooLargeToMultiply()
    {
        // the setter accepts any finite scale, so Filter has to have an answer for all of them:
        // a frame the game cannot advance by is still not a frame that takes the game down
        ScaledFrameTimeController frameTime = new() { TimeScale = double.MaxValue };

        Assert.Equal(TimeSpan.MaxValue, frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void StretchesFrameTime_AtALargeScaleThatStillFits()
    {
        // saturation is the last resort and not a shortcut: a scale that multiplies out to a
        // TimeSpan is still multiplied out, however large it is
        ScaledFrameTimeController frameTime = new() { TimeScale = 1_000_000 };

        Assert.Equal(TimeSpan.FromSeconds(16_000), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void FreezesTime_AtAnEnormousScale_WhenNoTimeHasPassed()
    {
        // no time times any scale is still no time; the frame loop can hand Filter a zero frame
        ScaledFrameTimeController frameTime = new() { TimeScale = double.MaxValue };

        Assert.Equal(TimeSpan.Zero, frameTime.Filter(TimeSpan.Zero));
    }

    [Fact]
    public void SaturatesBackwards_ForAFrameTimeThatRanBackwards()
    {
        // Filter is documented as taking elapsed time, so this is not a frame the loop produces.
        // It is here because the saturation has to be the whole of the answer: the multiplication
        // overflows in both directions, and only one of them being caught would leave the other
        // throwing the OverflowException this replaced.
        ScaledFrameTimeController frameTime = new() { TimeScale = double.MaxValue };

        Assert.Equal(TimeSpan.MinValue, frameTime.Filter(TimeSpan.FromMilliseconds(-16)));
    }

    [Fact]
    public void KeepsFilteringAfterAScaleIsRefused()
    {
        // the refusal is the whole of it: the controller is still the one the frame loop had
        ScaledFrameTimeController frameTime = new() { TimeScale = 0.5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => frameTime.TimeScale = double.PositiveInfinity);

        Assert.Equal(TimeSpan.FromMilliseconds(8), frameTime.Filter(TimeSpan.FromMilliseconds(16)));
    }
}
