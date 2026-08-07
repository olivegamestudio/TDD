namespace OliveGameStudio.Tests;

/// <summary>
/// Covers what a frame of touch reports before anything interprets it.
/// </summary>
/// <remarks>
/// The one thing worth pinning here is that there is no such thing as a touch frame with no
/// touches to read. Every other device frame in this seam is made of value types a host cannot
/// leave unset; this one carries a list, and a list has a way of being nothing at all.
/// </remarks>
public sealed class TouchFrameTests
{
    [Fact]
    public void NoPanelReportsNoTouchesRatherThanNothingAtAll()
    {
        Assert.Empty(TouchFrame.None.Points);
    }

    /// <summary>
    /// <c>default</c> is the frame a host gets by saying nothing, and it has to be readable — the
    /// frame path has no room to check whether the platform bothered.
    /// </summary>
    [Fact]
    public void AFrameNobodyFilledInReportsNoTouches()
    {
        TouchFrame frame = default;

        Assert.Empty(frame.Points);
    }

    [Fact]
    public void AFrameHandedNoListReportsNoTouches()
    {
        TouchFrame frame = new(null);

        Assert.Empty(frame.Points);
    }

    [Fact]
    public void AFrameReportsTheTouchesItWasGiven()
    {
        TouchPoint thumb = new(1, 120, 640);

        TouchFrame frame = new([thumb]);

        Assert.Equal(thumb, Assert.Single(frame.Points));
    }

    /// <summary>
    /// The identifier is the point of a touch point: it is what says the finger reading 640 this
    /// frame is the same finger that read 600 last frame, rather than a new one that happens to
    /// have landed nearby.
    /// </summary>
    [Fact]
    public void TouchesAreTheSameTouchWhenTheyAreTheSameFinger()
    {
        Assert.Equal(new TouchPoint(1, 120, 640), new TouchPoint(1, 120, 640));
        Assert.NotEqual(new TouchPoint(1, 120, 640), new TouchPoint(2, 120, 640));
    }

    /// <summary>
    /// A host that reads only a keyboard and a pad still composes, and reports no touches. The
    /// desktop host is exactly that host, and it must not have to know about a device it does not
    /// have.
    /// </summary>
    [Fact]
    public void AFrameOfInputWithoutTouchReportsNoTouches()
    {
        InputFrame frame = new(KeyboardFrame.None, GamePadFrame.None);

        Assert.Empty(frame.Touch.Points);
    }

    [Fact]
    public void AFrameOfInputCarriesTheTouchesItWasGiven()
    {
        InputFrame frame = new(
            KeyboardFrame.None,
            GamePadFrame.None,
            new TouchFrame([new TouchPoint(1, 120, 640)]));

        Assert.Single(frame.Touch.Points);
    }
}
