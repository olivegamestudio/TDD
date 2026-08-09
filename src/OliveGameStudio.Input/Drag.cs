namespace OliveGameStudio;

/// <summary>
/// A drag as it stands this frame: where it was picked up, where the pointer is now, and whether
/// it is still going.
/// </summary>
/// <param name="Phase">Whether anything is being dragged, and whether this is the frame it ended.</param>
/// <param name="FromX">Where the pointer went down, in pixels from the left edge of the window.</param>
/// <param name="FromY">Where the pointer went down, in pixels from the top edge of the window.</param>
/// <param name="X">Where the pointer is now, in pixels from the left edge of the window.</param>
/// <param name="Y">Where the pointer is now, in pixels from the top edge of the window.</param>
/// <remarks>
/// <para>
/// <b>Two places, and no opinion about what is at either of them.</b> A drag is where a press
/// started and where it ended; which slot those two places name is a question for whoever laid the
/// screen out, and answering it here would put the inventory's layout inside the input system.
/// That is the same split <c>TouchCircle</c> already takes — the overlay is handed where its
/// circles are rather than deciding.
/// </para>
/// <para>
/// <paramref name="FromX"/> and <paramref name="FromY"/> do not follow the pointer, deliberately.
/// The pick-up point is the only record of which slot the item came out of, and the pointer is by
/// then somewhere else — that is what a drag is.
/// </para>
/// <para>
/// On <see cref="DragPhase.Dropped"/> the pointer's position is the last one it was seen at rather
/// than one read from the frame the drop is reported on. A lifted finger is simply absent from the
/// next frame and a released mouse button says nothing about where the release happened, so the
/// last sighting is not an approximation of the drop point — it is the drop point.
/// </para>
/// </remarks>
public readonly record struct Drag(
    DragPhase Phase,
    double FromX,
    double FromY,
    double X,
    double Y)
{
    /// <summary>
    /// Nothing being dragged. What a screen nobody is touching reports.
    /// </summary>
    public static readonly Drag None = default;
}
