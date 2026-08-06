namespace OliveGameStudio;

/// <summary>
/// The tunable numbers of the loot magnet: how near the player has to come for a drop to notice
/// them, and how fast it travels once it has.
/// </summary>
/// <remarks>
/// <para>
/// A stats type for the mechanic the way <see cref="ShieldStats"/> is a shield's and
/// <see cref="OrbStats"/> is an orb's — everything that decides how loot reaches the player is
/// here, so tuning pickup is reading one type rather than hunting for the numbers wherever they
/// are applied.
/// </para>
/// <para>
/// Authored content, so it is a record and nothing here changes as it is played. Which drops
/// exist, and what falls out of what, are content of a different kind and live nowhere near this:
/// the engine provides the magnet, a game provides the loot.
/// </para>
/// </remarks>
/// <param name="Reach">
/// How near the player has to come, in world units, for a drop to be drawn toward them. Must be a
/// positive, finite number: a reach of zero would ask the player to land exactly on a drop, which
/// is a pickup they cannot perform without the button the design deliberately does not have, and
/// an endless one collects the whole world from where they are standing.
/// </param>
/// <param name="DriftSpeed">
/// How fast a drop travels toward the player once it has been drawn in, in world units per second.
/// Must be a positive, finite number: at zero a drop is drawn in and then never arrives, which is
/// the tease the guaranteed drop exists to rule out, and an endless one arrives before it can be
/// seen to move — which is the jump the drift is there instead of.
/// </param>
public sealed record LootMagnet(double Reach, double DriftSpeed)
{
    /// <inheritdoc cref="LootMagnet(double, double)" path="/param[@name='Reach']"/>
    /// <exception cref="ArgumentOutOfRangeException">The reach is not a positive, finite number.</exception>
    public double Reach { get; } = Usable(Reach, nameof(Reach));

    /// <inheritdoc cref="LootMagnet(double, double)" path="/param[@name='DriftSpeed']"/>
    /// <exception cref="ArgumentOutOfRangeException">The speed is not a positive, finite number.</exception>
    public double DriftSpeed { get; } = Usable(DriftSpeed, nameof(DriftSpeed));

    static double Usable(double value, string name)
    {
        // ThrowIfNegativeOrZero compares, and every comparison against NaN is false — so a NaN
        // walks past a range check written as one. It has to be named rather than ranged.
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                "A loot magnet's numbers have to be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, name);

        return value;
    }
}
