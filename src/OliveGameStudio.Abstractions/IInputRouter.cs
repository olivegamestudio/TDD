namespace OliveGameStudio;

/// <summary>
/// Decides where a frame's input goes: to the menu, or to the ship.
/// </summary>
/// <remarks>
/// <para>
/// Routing is UI first. A frame belongs to the user interface while something there is focused,
/// and falls through to the ship when nothing is. That order is what lets one key work a menu
/// button and fly the ship without the two ever fighting over it, and it is asked of the UI each
/// frame rather than remembered, because focus moves on its own.
/// </para>
/// <para>
/// The device is locked at the first press the menu accepts and stays locked for the session. It
/// is the same rule the player experiences as "the thing I started the game with is the thing I
/// play on", and it takes the place of arbitrating between devices every frame.
/// </para>
/// </remarks>
public interface IInputRouter
{
    /// <summary>
    /// Gets the device the game is being played on, or <see cref="ControlDevice.None"/> until one
    /// has been chosen.
    /// </summary>
    ControlDevice LockedTo { get; }

    /// <summary>
    /// Routes one frame of input.
    /// </summary>
    /// <param name="frame">Every device as the platform host read it this frame.</param>
    /// <remarks>
    /// Called once a frame, before the frame is updated, so what the game does this frame is
    /// decided by what the player is doing now rather than by what they were doing last frame.
    /// </remarks>
    void Route(InputFrame frame);
}
