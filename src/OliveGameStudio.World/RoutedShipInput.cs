namespace OliveGameStudio;

/// <summary>
/// The controls somebody else decided: it reports whatever was last put on it.
/// </summary>
/// <remarks>
/// <para>
/// The seam between input that is <em>pushed</em> and physics that <em>pulls</em>. The platform
/// host hands the game a frame of device state before the frame is updated, something upstream
/// decides what that means for the ship, and this holds the answer until the physics asks for it.
/// Without it the two halves could not meet: <see cref="IShipInput"/> is read on the frame path
/// and cannot go looking for a device, and a router cannot reach into physics that has not run yet.
/// </para>
/// <para>
/// It holds the last value rather than clearing after a read, because a frame that is drawn twice
/// or updated twice must see the same controls both times. Whoever routes is expected to write
/// every frame, including writing <see cref="ShipControls.Neutral"/> for a frame the ship should
/// not have — a stale value here is a ship that keeps flying after the player let go.
/// </para>
/// </remarks>
public sealed class RoutedShipInput : IShipInput
{
    /// <summary>
    /// Gets or sets what the pilot is asking for. Set by whatever routes input; read by the
    /// physics on the frame path.
    /// </summary>
    public ShipControls Controls { get; set; } = ShipControls.Neutral;

    /// <inheritdoc />
    public ShipControls Read() => Controls;
}
