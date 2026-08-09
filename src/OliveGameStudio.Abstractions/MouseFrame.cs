namespace OliveGameStudio;

/// <summary>
/// One frame of the mouse, as the platform host read it.
/// </summary>
/// <param name="Present">Whether this host has a mouse at all.</param>
/// <param name="X">How far across the window the pointer is, in pixels from the left edge.</param>
/// <param name="Y">How far down the window the pointer is, in pixels from the top edge.</param>
/// <param name="PrimaryHeld">
/// Whether the primary button is held — the one that picks something up and puts it down.
/// </param>
/// <remarks>
/// <para>
/// A position rather than anything meaning-level, for the same reason <see cref="TouchFrame"/>
/// carries one: where a pointer is has no meaning until something says what is drawn there.
/// <see cref="KeyboardFrame"/> can say <em>ahead</em> because a key is bound to an intent; a
/// pointer is bound to a place, and the place is the screen's to interpret.
/// </para>
/// <para>
/// Window pixels, y down, and the number is passed on unclamped — a pointer dragged past the edge
/// of the window is still the pointer the player is holding, and clipping it here would drop an
/// item back into the hold at the moment they reached past the frame.
/// </para>
/// <para>
/// <paramref name="Present"/> is carried rather than inferred, on exactly the reasoning
/// <see cref="GamePadFrame.Connected"/> is. A host with no mouse and a mouse parked in the corner
/// of the window with nothing pressed report the same three numbers, and they are not the same
/// thing: the first cannot pick anything up, and something reading a press off it would take hold
/// of whatever is drawn at the top-left corner.
/// </para>
/// <para>
/// One button, deliberately. A second button means a second meaning — a context menu, a quick
/// equip — and neither has been designed. Naming a button the game has no answer for would be a
/// value every reader has to handle and none can act on.
/// </para>
/// </remarks>
public readonly record struct MouseFrame(
    bool Present,
    double X,
    double Y,
    bool PrimaryHeld)
{
    /// <summary>
    /// No mouse at all. What a host without one reports, and what the tests start from.
    /// </summary>
    public static readonly MouseFrame None = default;
}
