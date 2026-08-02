namespace OliveGameStudio;

/// <summary>
/// Several input devices bound at once, reporting whichever one is actually being used.
/// </summary>
/// <remarks>
/// <para>
/// A player with a keyboard and a gamepad both plugged in is flying with one of them. Adding the
/// two answers together would give the ship something neither hand asked for — a stick half over
/// plus a held key reading as more than full thrust — so the devices are asked in order and the
/// first one asking for anything wins outright.
/// </para>
/// <para>
/// The arbitration is per device rather than per axis. Splitting it would let a stick supply the
/// helm while a key supplied the thrust, which is the summing this exists to avoid, only harder to
/// predict. Whichever device the player is using answers for both axes.
/// </para>
/// <para>
/// Stateless on purpose: it remembers nothing about which device was used last, so letting go of
/// one device hands control straight to the other rather than after a timeout the player cannot
/// see. It is read once a frame on the frame path, so it does no work beyond asking each device.
/// </para>
/// </remarks>
/// <param name="devices">
/// The devices to ask, in order of precedence. An empty list is legal and reports neutral, which
/// is the same answer as nobody at the controls.
/// </param>
public sealed class FirstActiveShipInput(params IShipInput[] devices) : IShipInput
{
    /// <summary>
    /// Gets the devices being asked, in the order they are asked in.
    /// </summary>
    /// <remarks>
    /// Exposed so a composition root can be checked to have bound what it meant to. Precedence is
    /// part of that: which device is asked first is a decision, not an implementation detail.
    /// </remarks>
    public IReadOnlyList<IShipInput> Devices { get; } = devices;

    /// <inheritdoc />
    public ShipControls Read()
    {
        foreach (IShipInput device in Devices)
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
