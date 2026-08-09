namespace OliveGameStudio;

/// <summary>
/// Which pointer this is, held for as long as it stays down.
/// </summary>
/// <param name="Device">The kind of device it came from.</param>
/// <param name="Index">
/// The platform's number for it: a finger's touch identifier, or zero for the mouse.
/// </param>
/// <remarks>
/// Identity without position, and the two are kept apart on purpose. A control that follows a
/// pointer has to ask "is this still the same one" every frame, and comparing coordinates cannot
/// answer it — a pointer that read 640 this frame and 600 the last is either the same one moving
/// or a second one that arrived nearby. Holding the identity in its own value is what lets a drag
/// compare pointers without comparing where they are.
/// </remarks>
public readonly record struct PointerId(PointerDevice Device, int Index)
{
    /// <summary>
    /// The mouse. There is only one, so it needs no number of its own.
    /// </summary>
    public static readonly PointerId Mouse = new(PointerDevice.Mouse, 0);

    /// <summary>
    /// The finger the platform gave this identifier.
    /// </summary>
    /// <param name="id">The platform's identifier for the finger.</param>
    /// <returns>That finger's identity.</returns>
    public static PointerId Finger(int id) => new(PointerDevice.Touch, id);
}
