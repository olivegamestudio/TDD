namespace OliveGameStudio;

/// <summary>
/// Several input devices behind one <see cref="IShipInput"/>: they are asked in order, and the
/// first one asking for anything answers for the frame outright.
/// </summary>
/// <remarks>
/// <para>
/// A game bound to both a keyboard and a gamepad has to decide what happens when both are
/// connected, and summing them is the wrong answer — two devices each asking for half a turn
/// would produce a full one, which is not what either hand did. Arbitrating per <em>device</em>
/// rather than per axis is also the more predictable of the two: letting a stick supply the helm
/// while a key supplies the throttle is the same summing in a form a player cannot see.
/// </para>
/// <para>
/// It holds no state between frames, so letting go of one device hands control to the next on the
/// very next frame rather than after a delay nobody asked for. Order is therefore the whole of the
/// policy, and it belongs to the composition root that binds the devices.
/// </para>
/// </remarks>
public sealed class FirstActiveShipInput : IShipInput
{
    readonly IShipInput[] _devices;

    /// <summary>
    /// Binds the given devices, in the order they should be asked.
    /// </summary>
    /// <param name="devices">
    /// The devices, most preferred first. None at all is allowed and reads as hands off, which is
    /// what a build with no input device bound should do.
    /// </param>
    /// <exception cref="ArgumentNullException">There is no device array.</exception>
    /// <exception cref="ArgumentException">One of the devices is missing.</exception>
    /// <remarks>
    /// The devices are copied, and a missing one is refused here rather than on the frame path.
    /// Both are about where a mistake surfaces: a caller that kept the array it passed could
    /// otherwise change which device is flying the ship mid-flight, and the only thing a null
    /// device could do is take the game down in the middle of a game.
    /// </remarks>
    public FirstActiveShipInput(params IShipInput[] devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        _devices = [.. devices];

        if (Array.IndexOf(_devices, null) >= 0)
        {
            throw new ArgumentException("A ship input device is missing.", nameof(devices));
        }
    }

    /// <summary>
    /// Gets the devices, in the order they are asked.
    /// </summary>
    public IReadOnlyList<IShipInput> Devices => _devices;

    /// <inheritdoc />
    public ShipControls Read()
    {
        foreach (IShipInput device in _devices)
        {
            ShipControls controls = device.Read();

            if (!controls.IsNeutral)
            {
                return controls;
            }
        }

        return ShipControls.Neutral;
    }
}
