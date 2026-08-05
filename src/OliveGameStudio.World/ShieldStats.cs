namespace OliveGameStudio;

/// <summary>
/// The tunable numbers of one shield: which of the two things a shield can do it does, and how much
/// of it.
/// </summary>
/// <remarks>
/// <para>
/// A stats type per object, the way <see cref="ShipHandling"/> is the ship's — everything a shield
/// is worth is here, so tuning one is reading one type rather than hunting for the numbers wherever
/// they are applied.
/// </para>
/// <para>
/// Built through <see cref="Absorbing"/> and <see cref="Reflecting"/> rather than a constructor
/// taking both numbers, because a shield does one thing. Left open, content could author an
/// absorption shield that also reflects — a third type nobody designed, arriving as a typo in a
/// data file rather than as a decision. The unused number stays zero and both are readable, so a
/// caller combining shields adds up two totals without asking each one what type it is.
/// </para>
/// <para>
/// Authored content, so it is a record: two shields with the same numbers are the same shield, and
/// nothing here changes as it is played. What <em>is</em> played — the layer depleting — belongs to
/// the <see cref="Ship"/> wearing it, not to the stats.
/// </para>
/// </remarks>
public sealed record ShieldStats
{
    ShieldStats(ShieldType type, double capacity, double reflectedFraction)
    {
        Type = type;
        Capacity = capacity;
        ReflectedFraction = reflectedFraction;
    }

    /// <summary>
    /// Gets what this shield does with a hit.
    /// </summary>
    public ShieldType Type { get; }

    /// <summary>
    /// Gets how much of the shield layer this shield contributes. Zero for a reflecting shield,
    /// which puts no layer in front of the hull.
    /// </summary>
    public double Capacity { get; }

    /// <summary>
    /// Gets the share of an incoming hit this shield sends back at the attacker, from 0 to 1. Zero
    /// for an absorption shield, which sends nothing back.
    /// </summary>
    public double ReflectedFraction { get; }

    /// <summary>
    /// Authors a shield that soaks damage — the layer that depletes before health.
    /// </summary>
    /// <param name="capacity">How much of the layer it is worth.</param>
    /// <returns>The shield's stats.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The capacity is not a positive, finite number. Zero or less is a fitted shield that does
    /// nothing, which reads at play time as a slot the player filled for no benefit; endless is a
    /// ship nothing can ever hurt. Both are content mistakes, and both are cheaper to find here than
    /// in a fight.
    /// </exception>
    public static ShieldStats Absorbing(double capacity)
    {
        ThrowIfNotFinite(capacity, nameof(capacity));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        return new ShieldStats(ShieldType.Absorption, capacity, reflectedFraction: 0);
    }

    /// <summary>
    /// Authors a shield that bounces damage back at whatever fired it.
    /// </summary>
    /// <param name="fraction">
    /// The share of an incoming hit it sends back, from just above 0 to 1. One means the whole hit
    /// goes back and nothing lands.
    /// </param>
    /// <returns>The shield's stats.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The share is not a finite number above 0 and at most 1. A share above one is not a stronger
    /// shield — it is damage invented on the way back out, and an attacker would be hit harder than
    /// it fired.
    /// </exception>
    public static ShieldStats Reflecting(double fraction)
    {
        ThrowIfNotFinite(fraction, nameof(fraction));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fraction);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fraction, 1);

        return new ShieldStats(ShieldType.Reflection, capacity: 0, reflectedFraction: fraction);
    }

    // ThrowIfNegativeOrZero compares, and every comparison against NaN is false — so a NaN walks
    // past every range check written as one. It has to be named rather than ranged.
    static void ThrowIfNotFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "A shield's stats have to be finite numbers.");
        }
    }
}
