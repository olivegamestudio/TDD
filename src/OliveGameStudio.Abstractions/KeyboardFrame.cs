namespace OliveGameStudio;

/// <summary>
/// One frame of the keyboard, as the platform host read it.
/// </summary>
/// <param name="Ahead">Whether the ahead key is held.</param>
/// <param name="Astern">Whether the astern key is held.</param>
/// <param name="Port">Whether the turn-to-port key is held.</param>
/// <param name="Starboard">Whether the turn-to-starboard key is held.</param>
/// <param name="Confirm">Whether the confirm key is held — the one that works a menu button.</param>
/// <remarks>
/// Named for what the player is asking for rather than for the keys that ask it, because which
/// keys those are is the host's decision and nothing downstream of it should be able to see the
/// answer. That keeps the split the engine already uses for saved progress and for ship input: the
/// host provides the device, the engine provides what an input means.
/// </remarks>
public readonly record struct KeyboardFrame(
    bool Ahead,
    bool Astern,
    bool Port,
    bool Starboard,
    bool Confirm)
{
    /// <summary>
    /// A keyboard nobody is touching. What a host with no keyboard reports, and what the tests
    /// start from.
    /// </summary>
    public static readonly KeyboardFrame None = default;
}
