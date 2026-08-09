namespace OliveGameStudio;

/// <summary>
/// Everything the platform host read from its devices this frame, handed to the game in one piece.
/// </summary>
/// <param name="Keyboard">The keyboard as it stands this frame.</param>
/// <param name="GamePad">The gamepad as it stands this frame.</param>
/// <param name="Touch">The touch screen as it stands this frame. Defaults to nobody touching it.</param>
/// <param name="Mouse">The mouse as it stands this frame. Defaults to the host not having one.</param>
/// <remarks>
/// Every device in one snapshot rather than one call per device, because the routing decision is
/// made across all of them at once: which device is flying is settled by comparing them, and a
/// frame that arrived a device at a time would have to be reassembled before that question could
/// be asked. It also means the host reads each device exactly once a frame, so nothing downstream
/// can see two different answers about the same instant.
/// <para>
/// <paramref name="Touch"/> and <paramref name="Mouse"/> have defaults because a host is not
/// expected to have every device: a desktop host with no touch screen says nothing about one and
/// reports a frame that is honest about having read no fingers, rather than being made to name a
/// device it does not have. A host with neither is unchanged by both of them existing.
/// </para>
/// </remarks>
public readonly record struct InputFrame(
    KeyboardFrame Keyboard,
    GamePadFrame GamePad,
    TouchFrame Touch = default,
    MouseFrame Mouse = default)
{
    /// <summary>
    /// A frame in which nobody touched anything.
    /// </summary>
    public static readonly InputFrame None = default;
}
