namespace OliveGameStudio;

/// <summary>
/// An input device with nobody at it: it always reports <see cref="ShipControls.Neutral"/>.
/// </summary>
/// <remarks>
/// The engine's default, so a game composes and runs before a platform host has bound a real
/// keyboard or gamepad. The ship then sits still, which is a visible and correct answer to "no
/// pilot" — better than a container that refuses to build, or a frame path that has to keep
/// checking whether an input device turned up.
/// </remarks>
public sealed class NeutralShipInput : IShipInput
{
    /// <inheritdoc />
    public ShipControls Read() => ShipControls.Neutral;
}
