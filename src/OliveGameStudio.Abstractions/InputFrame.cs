namespace OliveGameStudio;

/// <summary>
/// Everything the platform host read from its devices this frame, handed to the game in one piece.
/// </summary>
/// <param name="Keyboard">The keyboard as it stands this frame.</param>
/// <param name="GamePad">The gamepad as it stands this frame.</param>
/// <remarks>
/// Every device in one snapshot rather than one call per device, because the routing decision is
/// made across all of them at once: which device is flying is settled by comparing them, and a
/// frame that arrived a device at a time would have to be reassembled before that question could
/// be asked. It also means the host reads each device exactly once a frame, so nothing downstream
/// can see two different answers about the same instant.
/// </remarks>
public readonly record struct InputFrame(KeyboardFrame Keyboard, GamePadFrame GamePad)
{
    /// <summary>
    /// A frame in which nobody touched anything.
    /// </summary>
    public static readonly InputFrame None = default;
}
