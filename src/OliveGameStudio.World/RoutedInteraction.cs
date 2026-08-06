namespace OliveGameStudio;

/// <summary>
/// What the player asked for this frame that was not flying: to talk to whoever is in front of
/// them, or to be out of the conversation they are in.
/// </summary>
/// <remarks>
/// <para>
/// The same seam as <see cref="RoutedShipInput"/> — input is pushed by the platform host, the game
/// pulls it on the frame path — with one difference that matters. Ship controls are a
/// <em>state</em>: holding the thrust key means thrust every frame it is held. These are
/// <em>edges</em>: a held interact key asks to open one conversation, not one a frame for as long
/// as a finger rests on it. Whoever routes writes both members every frame, so a request that was
/// not acted on cannot be found still sitting here on the next one.
/// </para>
/// <para>
/// The two are exclusive by routing rather than by anything here: interact is the question asked
/// while the player is flying, and cancel the one asked while something on screen holds the
/// controls.
/// </para>
/// </remarks>
public sealed class RoutedInteraction
{
    /// <summary>
    /// Gets or sets a value indicating whether the player asked to interact on this frame.
    /// </summary>
    public bool Interact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the player asked to back out on this frame.
    /// </summary>
    public bool Cancel { get; set; }
}
