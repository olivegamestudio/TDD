namespace OliveGameStudio;

/// <summary>
/// Which of the devices the platform host reads is the one the player is playing on.
/// </summary>
/// <remarks>
/// A device rather than a control <em>scheme</em>: this names where input comes from, never what
/// the player asked for. Two schemes on one keyboard would still be <see cref="Keyboard"/>, and
/// choosing between schemes is a settings model that does not exist yet.
/// </remarks>
public enum ControlDevice
{
    /// <summary>
    /// No device has been chosen yet. The state the game starts in, before anything has been
    /// pressed, and the reason the ship sits still on the company screen.
    /// </summary>
    None,

    /// <summary>
    /// The keyboard — digital input, where a key is held or it is not.
    /// </summary>
    Keyboard,

    /// <summary>
    /// A gamepad — analogue input, where a stick is wherever it has been pushed to.
    /// </summary>
    GamePad,
}
