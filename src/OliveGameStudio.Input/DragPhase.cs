namespace OliveGameStudio;

/// <summary>
/// Where a drag has got to this frame.
/// </summary>
public enum DragPhase
{
    /// <summary>
    /// Nothing is being dragged. No pointer is down, or none has been picked up yet.
    /// </summary>
    None,

    /// <summary>
    /// A pointer is down and carrying something. Reported on every frame of the drag, including
    /// the one it started on.
    /// </summary>
    Dragging,

    /// <summary>
    /// The pointer has been let go. Reported on exactly one frame — the frame the pointer stopped
    /// being down — because putting an item into a slot is something that happens once, and a
    /// phase that stayed on would do it again on every frame the player left the pointer resting
    /// where they dropped it.
    /// </summary>
    Dropped,
}
