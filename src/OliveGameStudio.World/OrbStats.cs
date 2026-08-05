namespace OliveGameStudio;

/// <summary>
/// The tunable numbers of one orb: which of the things an orb can do it does, how far from the ship
/// it keeps, and how fast it goes round.
/// </summary>
/// <remarks>
/// <para>
/// A stats type per object, the way <see cref="ShipHandling"/> is the ship's and
/// <see cref="ShieldStats"/> is a shield's — everything an orb is worth is here, so tuning one is
/// reading one type rather than hunting for the numbers wherever they are applied.
/// </para>
/// <para>
/// Built through <see cref="Orbiting"/> and <see cref="Tracking"/> rather than a constructor taking
/// every number, because an orb does one thing. Left open, content could author a tracker that also
/// circles — a behaviour nobody designed, arriving as a typo in a data file rather than as a
/// decision. The number a behaviour does not use stays zero and both are readable, so a caller
/// placing the slots works one formula rather than asking each orb what kind it is.
/// </para>
/// <para>
/// Authored content, so it is a record: two orbs with the same numbers are the same orb, and
/// nothing here changes as it is played. Where an orb <em>is</em> at a given moment is not held
/// here at all — see <see cref="Orbs.PlaceAround"/>, which works it out rather than remembering it.
/// </para>
/// </remarks>
public sealed record OrbStats
{
    OrbStats(OrbBehaviour behaviour, double radius, double angularSpeed)
    {
        Behaviour = behaviour;
        Radius = radius;
        AngularSpeed = angularSpeed;
    }

    /// <summary>
    /// Gets what this orb does once it is fitted.
    /// </summary>
    public OrbBehaviour Behaviour { get; }

    /// <summary>
    /// Gets how far from the ship this orb keeps, in world units.
    /// </summary>
    public double Radius { get; }

    /// <summary>
    /// Gets how fast this orb goes round the ship, in radians per second, positive to starboard.
    /// Zero for a tracker, which holds its station rather than circling it.
    /// </summary>
    public double AngularSpeed { get; }

    /// <summary>
    /// Authors an orbiting defence — a companion that circles the ship and never leaves it.
    /// </summary>
    /// <param name="radius">How far out it circles, in world units.</param>
    /// <param name="angularSpeed">
    /// How fast it goes round, in radians per second. The sign is which way: positive turns to
    /// starboard, negative to port, and both are ordinary content.
    /// </param>
    /// <returns>The orb's stats.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The radius is not a positive, finite number, or the rate is not a finite number other than
    /// zero. A radius of zero puts the companion inside the ship, where the player never sees it; an
    /// endless one puts it where the ship never is. A rate of zero is a fitted orbiting defence that
    /// does not orbit — a slot filled for no benefit, which is the answer a shield with no capacity
    /// already gets.
    /// </exception>
    public static OrbStats Orbiting(double radius, double angularSpeed)
    {
        ThrowIfNotAUsableRadius(radius);

        ThrowIfNotFinite(angularSpeed, nameof(angularSpeed));
        if (angularSpeed == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(angularSpeed),
                angularSpeed,
                "An orbiting orb has to go round; zero is an orb that holds station.");
        }

        return new OrbStats(OrbBehaviour.Orbiting, radius, angularSpeed);
    }

    /// <summary>
    /// Authors an autonomous attacker — a companion that leaves station to go after a target.
    /// </summary>
    /// <remarks>
    /// It takes a radius and no rate because a tracker does not circle: the distance is where it
    /// waits, and where it goes from there is a question about what it is tracking. Nothing in the
    /// world is hostile yet, so that answer belongs with the fight rather than being invented here.
    /// </remarks>
    /// <param name="radius">How far from the ship it waits, in world units.</param>
    /// <returns>The orb's stats.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The radius is not a positive, finite number, for the reason an orbiting orb's is not.
    /// </exception>
    public static OrbStats Tracking(double radius)
    {
        ThrowIfNotAUsableRadius(radius);

        return new OrbStats(OrbBehaviour.Tracking, radius, angularSpeed: 0);
    }

    static void ThrowIfNotAUsableRadius(double radius)
    {
        ThrowIfNotFinite(radius, nameof(radius));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius, nameof(radius));
    }

    // ThrowIfNegativeOrZero compares, and every comparison against NaN is false — so a NaN walks
    // past every range check written as one. It has to be named rather than ranged.
    static void ThrowIfNotFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "An orb's stats have to be finite numbers.");
        }
    }
}
