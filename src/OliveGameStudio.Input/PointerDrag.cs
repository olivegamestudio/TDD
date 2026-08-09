namespace OliveGameStudio;

/// <summary>
/// Turns a frame of pointers into a drag: what was picked up, where it is being carried, and where
/// it was let go.
/// </summary>
/// <remarks>
/// <para>
/// This is the gesture half of the drag-and-drop the loadout and the hold are worked with. It
/// knows nothing about slots, items or what is drawn where — it reports two places and the phase
/// the gesture is in, and the screen that laid the slots out is what turns those places into a
/// source and a target. Keeping it here rather than on a screen is what makes one gesture serve
/// the inventory, the loadout, and anything else later dragged about, and what lets it be covered
/// without a window.
/// </para>
/// <para>
/// <b>The drag captures its pointer</b>, exactly as <see cref="TouchOverlay"/>'s helm captures the
/// thumb that took it, and for a reason of the same shape: an item being carried belongs to the
/// hand carrying it. A second finger landing mid-drag — the other hand, a palm, the player
/// steadying the device — must not take the item out of the first one's hand, and a mouse on a
/// machine that has both must not either. The captured pointer is followed by its identifier and
/// never by where it is, which is what keeps the drag alive when the platform reports another
/// pointer ahead of it.
/// </para>
/// <para>
/// <b>The drop is an edge, so this is called once a frame.</b> That is the same obligation
/// <see cref="InputRouter"/> carries for a press, and for the same reason: the phase a gesture
/// <em>enters</em> is not something the frame can be asked for twice. A drag in progress does read
/// the same however many times its frame is read — only the drop is consumed.
/// </para>
/// <para>
/// <b>The mouse is asked first when the drag is free.</b> A machine with both devices has to pick
/// one and neither is more right; stating the order is what stops it depending on the order the
/// platform happened to report things in. Once a pointer has the drag the order stops mattering,
/// which is the case that was actually worth deciding.
/// </para>
/// <para>
/// A pointer the host could not place — a coordinate that is not a number — is carried through
/// rather than refused, on the same reading <see cref="TouchCircle.Contains"/> takes: it drops on
/// nothing, because an ordered comparison against <c>NaN</c> is false whichever way round it is
/// written, so every hit-test the drop is put to answers "not this slot". Refusing it here would
/// take the game down on the frame path for one bad reading from a driver.
/// </para>
/// </remarks>
public sealed class PointerDrag
{
    /// <summary>
    /// The pointer carrying the drag, or <see langword="null"/> if nothing is being dragged.
    /// </summary>
    PointerId? _held;

    double _fromX;
    double _fromY;
    double _atX;
    double _atY;

    /// <summary>
    /// Reads one frame of pointers.
    /// </summary>
    /// <param name="frame">Every device as the platform host read it this frame.</param>
    /// <returns>
    /// The drag as it stands: <see cref="DragPhase.None"/> when nothing is being dragged,
    /// <see cref="DragPhase.Dragging"/> while a pointer carries one, and
    /// <see cref="DragPhase.Dropped"/> on the single frame it is let go.
    /// </returns>
    /// <remarks>
    /// Called once a frame, before the frame is updated, so the screen acts on where the pointer
    /// is now rather than where it was.
    /// </remarks>
    public Drag Read(InputFrame frame)
    {
        if (_held is { } held)
        {
            return Carrying(frame, held);
        }

        if (FirstDown(frame) is not { } taken)
        {
            return Drag.None;
        }

        _held = taken.Id;
        _fromX = _atX = taken.X;
        _fromY = _atY = taken.Y;

        return new Drag(DragPhase.Dragging, _fromX, _fromY, _atX, _atY);
    }

    /// <summary>
    /// What the drag already in progress is doing this frame: still going, or just let go.
    /// </summary>
    /// <remarks>
    /// A pointer that has stopped being down is not reported at all — a finger's lift is its
    /// absence and the mouse's is its button — so the frame it is missing from is the frame it was
    /// released on, and the position it was last seen at is where that happened.
    /// </remarks>
    Drag Carrying(InputFrame frame, PointerId held)
    {
        if (Find(frame, held) is { } pointer)
        {
            _atX = pointer.X;
            _atY = pointer.Y;

            return new Drag(DragPhase.Dragging, _fromX, _fromY, _atX, _atY);
        }

        _held = null;

        return new Drag(DragPhase.Dropped, _fromX, _fromY, _atX, _atY);
    }

    /// <summary>
    /// The named pointer, if it is still down this frame.
    /// </summary>
    static Pointer? Find(InputFrame frame, PointerId id)
    {
        if (id.Device == PointerDevice.Mouse)
        {
            return MouseDown(frame);
        }

        foreach (TouchPoint touch in frame.Touch.Points)
        {
            if (touch.Id == id.Index)
            {
                return new Pointer(id, touch.X, touch.Y);
            }
        }

        return null;
    }

    /// <summary>
    /// The pointer a free drag takes: the mouse if its button is down, otherwise the first finger
    /// the host reported.
    /// </summary>
    static Pointer? FirstDown(InputFrame frame)
    {
        if (MouseDown(frame) is { } mouse)
        {
            return mouse;
        }

        foreach (TouchPoint touch in frame.Touch.Points)
        {
            return new Pointer(PointerId.Finger(touch.Id), touch.X, touch.Y);
        }

        return null;
    }

    /// <summary>
    /// The mouse as a pointer, or <see langword="null"/> when there is no mouse or its button is
    /// not held.
    /// </summary>
    /// <remarks>
    /// Presence is checked as well as the button, because a host with no mouse reports whatever
    /// zeroes its frame was built from. Taking a drag from one would pick up whatever is drawn in
    /// the top-left corner of the window, which is the same mistake taking a confirm from an
    /// unplugged pad would be.
    /// </remarks>
    static Pointer? MouseDown(InputFrame frame) =>
        frame.Mouse is { Present: true, PrimaryHeld: true } mouse
            ? new Pointer(PointerId.Mouse, mouse.X, mouse.Y)
            : null;
}
