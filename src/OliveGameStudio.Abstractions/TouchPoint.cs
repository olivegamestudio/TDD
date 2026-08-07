namespace OliveGameStudio;

/// <summary>
/// One finger on the screen this frame, as the platform host read it.
/// </summary>
/// <param name="Id">
/// The platform's identifier for this finger, held for as long as it stays down.
/// </param>
/// <param name="X">How far across the window the finger is, in pixels from the left edge.</param>
/// <param name="Y">How far down the window the finger is, in pixels from the top edge.</param>
/// <remarks>
/// <para>
/// <paramref name="Id"/> is carried because a touch is not a position: it is a position over time.
/// A finger that read 640 this frame and 600 the last is either the same thumb sliding up the
/// screen or a second one that landed nearby, and nothing about the two coordinates says which.
/// Only the identifier does, and a control that has to know it is still the same finger — an
/// overlay stick, a drag — cannot be built without it.
/// </para>
/// <para>
/// Window pixels rather than anything world-shaped, and y down, because that is the space the
/// device reports in and the space an overlay drawn over the window is placed in. Turning a touch
/// into something the world understands is the job of whatever owns the overlay, and it needs the
/// unconverted number to do it.
/// </para>
/// </remarks>
public readonly record struct TouchPoint(int Id, double X, double Y);
