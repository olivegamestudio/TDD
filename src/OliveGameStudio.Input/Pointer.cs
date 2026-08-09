namespace OliveGameStudio;

/// <summary>
/// One pointer that is down this frame — a finger on the screen or the mouse with its button held
/// — in the terms a screen can hit-test against.
/// </summary>
/// <param name="Id">Which pointer this is, so it can be followed between frames.</param>
/// <param name="X">How far across the window it is, in pixels from the left edge.</param>
/// <param name="Y">How far down the window it is, in pixels from the top edge.</param>
/// <remarks>
/// <para>
/// This is what makes "drag-and-drop serves mouse and touch" one piece of code rather than two. A
/// mouse and a finger arrive on the input seam in different shapes because they are different
/// devices — the mouse says whether its button is down, a finger says nothing because being
/// reported at all <em>is</em> it being down — and everything past that difference is the same
/// gesture. Flattening them here is what keeps the difference from reaching a screen, exactly as
/// <c>ShipControls</c> keeps the difference between a stick and a key from reaching the physics.
/// </para>
/// <para>
/// A pointer that is not down is not a pointer. There is no hover here and no "moved with nothing
/// pressed": a finger that is not touching the screen has no position at all, so a model that
/// carried one could only ever be a mouse-shaped model with touch bolted to the side of it.
/// </para>
/// <para>
/// Window pixels and y down, because that is the space both devices report in and the space
/// anything drawn over the window is laid out in.
/// </para>
/// </remarks>
public readonly record struct Pointer(PointerId Id, double X, double Y);
