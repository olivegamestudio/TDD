using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A pilot that holds the controls wherever a test puts them, standing in for the keyboard or
/// gamepad the platform host binds in the real game.
/// </summary>
public sealed class FixedShipInput : IShipInput
{
    /// <summary>
    /// Gets or sets what the pilot is asking for. Changing it takes effect on the next frame.
    /// </summary>
    public ShipControls Controls { get; set; } = ShipControls.Neutral;

    /// <inheritdoc />
    public ShipControls Read() => Controls;
}
