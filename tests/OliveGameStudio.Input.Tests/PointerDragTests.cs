namespace OliveGameStudio.Input.Tests;

/// <summary>
/// Covers the drag gesture the inventory and loadout screens are to be built on: what a pointer
/// picked up, where it is now, and where it was let go.
/// </summary>
/// <remarks>
/// Nothing here knows what is drawn under the pointer, and that is the point — a drag is where a
/// press started and where it ended, and which slot those two places name is a question for
/// whoever laid the screen out. The coordinates are window pixels, so the numbers below are
/// arbitrary; what is being pinned is the relationship between them.
/// </remarks>
public sealed class PointerDragTests
{
    readonly PointerDrag _drag = new();

    static InputFrame Mouse(double x, double y, bool held) =>
        new(default, default, default, new MouseFrame(Present: true, X: x, Y: y, PrimaryHeld: held));

    static InputFrame Fingers(params TouchPoint[] points) =>
        new(default, default, new TouchFrame(points));

    [Fact]
    public void NothingIsBeingDraggedWhenNothingIsDown()
    {
        Drag drag = _drag.Read(InputFrame.None);

        Assert.Equal(DragPhase.None, drag.Phase);
    }

    /// <summary>
    /// A host that names no devices at all — <c>default</c> rather than a frame it filled in — is
    /// nobody dragging anything. This is read on the frame path, where there is no room to check
    /// whether the platform bothered.
    /// </summary>
    [Fact]
    public void AFrameTheHostNeverFilledInIsNobodyDragging()
    {
        Drag drag = _drag.Read(default);

        Assert.Equal(Drag.None, drag);
    }

    [Fact]
    public void PressingTheMouseStartsADragWhereItWasPressed()
    {
        Drag drag = _drag.Read(Mouse(120, 340, held: true));

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(340, drag.FromY);
        Assert.Equal(120, drag.X);
        Assert.Equal(340, drag.Y);
    }

    /// <summary>
    /// The pick-up point is what says which slot the item came out of, so it has to survive every
    /// frame of the drag rather than following the pointer.
    /// </summary>
    [Fact]
    public void MovingTheMouseKeepsWhereTheDragStarted()
    {
        _drag.Read(Mouse(120, 340, held: true));

        Drag drag = _drag.Read(Mouse(500, 200, held: true));

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(340, drag.FromY);
        Assert.Equal(500, drag.X);
        Assert.Equal(200, drag.Y);
    }

    [Fact]
    public void LettingTheMouseGoDropsItWhereThePointerLastWas()
    {
        _drag.Read(Mouse(120, 340, held: true));
        _drag.Read(Mouse(500, 200, held: true));

        Drag drag = _drag.Read(Mouse(500, 200, held: false));

        Assert.Equal(DragPhase.Dropped, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(340, drag.FromY);
        Assert.Equal(500, drag.X);
        Assert.Equal(200, drag.Y);
    }

    /// <summary>
    /// The drop is an edge and is reported once. A screen that acted on it every frame would move
    /// the item into the slot, and then keep moving it for as long as the player left the pointer
    /// where they dropped it.
    /// </summary>
    [Fact]
    public void ADropIsReportedOnceAndThenTheDragIsOver()
    {
        _drag.Read(Mouse(120, 340, held: true));
        _drag.Read(Mouse(120, 340, held: false));

        Drag drag = _drag.Read(Mouse(120, 340, held: false));

        Assert.Equal(DragPhase.None, drag.Phase);
    }

    /// <summary>
    /// A mouse being moved about with nothing pressed is not dragging anything, however far it
    /// travels. The press is what picks an item up.
    /// </summary>
    [Fact]
    public void AMouseThatWasNeverSeenDownStartsNoDrag()
    {
        Drag drag = _drag.Read(Mouse(120, 340, held: false));

        Assert.Equal(DragPhase.None, drag.Phase);
    }

    /// <summary>
    /// A mouse the host does not have reports nothing pressed, and its coordinates mean nothing.
    /// Taking a drag from one would pick an item up at the top-left corner of the window, which is
    /// the same mistake taking a confirm from an unplugged pad would be.
    /// </summary>
    [Fact]
    public void AMouseTheHostDoesNotHaveDragsNothing()
    {
        InputFrame frame = new(
            default,
            default,
            default,
            new MouseFrame(Present: false, X: 120, Y: 340, PrimaryHeld: true));

        Drag drag = _drag.Read(frame);

        Assert.Equal(DragPhase.None, drag.Phase);
    }

    [Fact]
    public void AFingerDragsExactlyAsAMouseDoes()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340)));
        Drag moved = _drag.Read(Fingers(new TouchPoint(7, 500, 200)));

        Drag dropped = _drag.Read(Fingers());

        Assert.Equal(DragPhase.Dragging, moved.Phase);
        Assert.Equal(120, moved.FromX);
        Assert.Equal(500, moved.X);

        Assert.Equal(DragPhase.Dropped, dropped.Phase);
        Assert.Equal(120, dropped.FromX);
        Assert.Equal(340, dropped.FromY);
        Assert.Equal(500, dropped.X);
        Assert.Equal(200, dropped.Y);
    }

    /// <summary>
    /// A lifted finger is simply absent from the next frame, so the last place it was seen is the
    /// only record of where it was let go. That is why the drag carries the position forward
    /// rather than reading it from the frame the drop is reported on.
    /// </summary>
    [Fact]
    public void ALiftedFingerDropsWhereItWasLastSeen()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340)));
        _drag.Read(Fingers(new TouchPoint(7, 640, 480)));

        Drag drag = _drag.Read(InputFrame.None);

        Assert.Equal(DragPhase.Dropped, drag.Phase);
        Assert.Equal(640, drag.X);
        Assert.Equal(480, drag.Y);
    }

    /// <summary>
    /// The drag captures the pointer that took it, exactly as the overlay's helm captures the
    /// thumb that took it. A second finger landing mid-drag — the other hand, a palm, the player
    /// steadying the device — must not take the item out of the first one's hand.
    /// </summary>
    [Fact]
    public void ASecondFingerCannotTakeADragInProgress()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340)));

        Drag drag = _drag.Read(Fingers(
            new TouchPoint(9, 900, 900),
            new TouchPoint(7, 130, 350)));

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(130, drag.X);
        Assert.Equal(350, drag.Y);
    }

    /// <summary>
    /// The captured pointer is followed by its identifier and not by where it is, which is what
    /// keeps a drag going when another finger is reported ahead of it.
    /// </summary>
    [Fact]
    public void LiftingTheOtherFingerLeavesTheDragAlone()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340), new TouchPoint(9, 900, 900)));

        Drag drag = _drag.Read(Fingers(new TouchPoint(7, 200, 400)));

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(200, drag.X);
    }

    /// <summary>
    /// Once the captured pointer lifts, the drag is free again and the next frame's first pointer
    /// takes it. The finger that was ignored throughout the first drag is an ordinary candidate
    /// now, and the new drag starts where that finger is when it is taken up — not where it first
    /// landed, which was a moment it was not dragging anything.
    /// </summary>
    [Fact]
    public void ADragIsFreeAgainOnceItsPointerLifts()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340)));
        _drag.Read(Fingers(new TouchPoint(9, 900, 900)));

        Drag taken = _drag.Read(Fingers(new TouchPoint(9, 910, 910)));
        Drag moved = _drag.Read(Fingers(new TouchPoint(9, 920, 920)));

        Assert.Equal(DragPhase.Dragging, taken.Phase);
        Assert.Equal(910, taken.FromX);
        Assert.Equal(910, taken.FromY);

        Assert.Equal(910, moved.FromX);
        Assert.Equal(920, moved.X);
    }

    /// <summary>
    /// A machine with both devices does not let one interrupt the other. The mouse is asked first
    /// when the drag is free, and a finger landing on a mouse drag changes nothing.
    /// </summary>
    [Fact]
    public void AFingerCannotTakeADragTheMouseIsHolding()
    {
        _drag.Read(Mouse(120, 340, held: true));

        InputFrame both = new(
            default,
            default,
            new TouchFrame([new TouchPoint(7, 900, 900)]),
            new MouseFrame(Present: true, X: 130, Y: 350, PrimaryHeld: true));

        Drag drag = _drag.Read(both);

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(130, drag.X);
    }

    /// <summary>
    /// And the mouse cannot take one a finger is holding either. The capture is about the pointer,
    /// not about which device is preferred when the drag is free.
    /// </summary>
    [Fact]
    public void TheMouseCannotTakeADragAFingerIsHolding()
    {
        _drag.Read(Fingers(new TouchPoint(7, 120, 340)));

        InputFrame both = new(
            default,
            default,
            new TouchFrame([new TouchPoint(7, 130, 350)]),
            new MouseFrame(Present: true, X: 900, Y: 900, PrimaryHeld: true));

        Drag drag = _drag.Read(both);

        Assert.Equal(DragPhase.Dragging, drag.Phase);
        Assert.Equal(120, drag.FromX);
        Assert.Equal(130, drag.X);
    }

    /// <summary>
    /// A drag in progress reads the same however many times the frame is read, because nothing
    /// about it changes until the pointers do. Only the drop is an edge.
    /// </summary>
    [Fact]
    public void ReadingTheSameFrameTwiceMidDragGivesTheSameAnswer()
    {
        InputFrame frame = Mouse(120, 340, held: true);

        Drag first = _drag.Read(frame);
        Drag second = _drag.Read(frame);

        Assert.Equal(first, second);
    }
}
