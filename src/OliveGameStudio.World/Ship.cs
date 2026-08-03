namespace OliveGameStudio;

/// <summary>
/// A ship in flight: one hull, built from a <see cref="ShipProfile"/>, with the condition it is in
/// and the physics that carries it around the world.
/// </summary>
/// <remarks>
/// <para>
/// A ship is transient. It belongs to the game currently being played, not to the player: a
/// character owns a profile and a loadout and survives losing the hull, so nothing here is
/// persisted. Starting or resuming a game builds a new one, which is why a resumed game begins at
/// rest facing forward without anybody having to remember to reset it.
/// </para>
/// <para>
/// <b>The flown ship and the owned ship cannot disagree.</b> <see cref="Movement"/> is built here
/// from <see cref="Handling"/> rather than registered beside it, so there is no way to give the
/// physics one set of numbers and the ship another. That is the single reason this type owns the
/// physics rather than sitting next to it.
/// </para>
/// </remarks>
public sealed class Ship
{
    /// <summary>
    /// Builds a ship from the hull content describes.
    /// </summary>
    /// <param name="profile">The hull to build.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The profile could not be flown or could not be built — see <see cref="ShipMovement"/> for
    /// the handling it refuses and <see cref="Meter"/> for the pools it refuses.
    /// </exception>
    public Ship(ShipProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
        Loadout = new Inventory(profile.Loadout);
        Health = new Meter(profile.Health);
        Durability = new Meter(profile.Durability);

        // Nothing fitted supplies a shield yet, so every ship starts with none rather than with a
        // shield the content never gave it. A ship with no shield and a ship whose shield is down
        // read the same here on purpose: both are one hit from the hull.
        Shield = new Meter(0);

        Movement = new ShipMovement(profile.Handling);
    }

    /// <summary>
    /// Gets the hull this ship was built from.
    /// </summary>
    public ShipProfile Profile { get; }

    /// <summary>
    /// Gets how the ship flies.
    /// </summary>
    public ShipHandling Handling => Profile.Handling;

    /// <summary>
    /// Gets what the ship is fitted with. Items are equipped from the character's inventory; which
    /// slots exist and what may go in them is an open decision, so today this is what is fitted and
    /// nothing more.
    /// </summary>
    public Inventory Loadout { get; }

    /// <summary>
    /// Gets the hull's own health, and what is left of it.
    /// </summary>
    public Meter Health { get; }

    /// <summary>
    /// Gets the shield the ship is carrying, and what is left of it. It comes from what is fitted,
    /// so a ship with nothing fitted has a maximum of zero.
    /// </summary>
    public Meter Shield { get; }

    /// <summary>
    /// Gets the hull's structural condition, and what is left of it.
    /// </summary>
    public Meter Durability { get; }

    /// <summary>
    /// Gets the physics that flies this ship, built from its own <see cref="Handling"/>.
    /// </summary>
    public ShipMovement Movement { get; }
}
