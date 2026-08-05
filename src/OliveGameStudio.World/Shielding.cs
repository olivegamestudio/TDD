namespace OliveGameStudio;

/// <summary>
/// What is in a ship's shield slots, and what filling them adds up to.
/// </summary>
/// <remarks>
/// <para>
/// The slots are filled freely: any shield can go in either. <b>The stacking rule is that there
/// isn't one</b> — slots add up, so two of a kind double what one was worth and a mix gives both
/// effects at single strength, without either being written down as a special case. A rule that
/// said "double when they match" would have to answer what happens when two shields nearly match,
/// and adding is the answer that never has to.
/// </para>
/// <para>
/// The slot count is fixed here rather than being a ship stat because v1 hulls all carry the same
/// layout. When a hull is allowed to vary it, <see cref="Slots"/> becomes a number the hull
/// supplies and this type takes it — which is a change to where the number comes from, not to what
/// filling the slots means.
/// </para>
/// <para>
/// Immutable, and separate from the layer it produces. This says what the ship is wearing;
/// <see cref="Ship.Shield"/> is how much of the layer is left, and that belongs to the ship being
/// shot at rather than to the shields, so two ships wearing the same shields do not share a pool.
/// </para>
/// </remarks>
public sealed class Shielding
{
    /// <summary>
    /// How many shields a ship can carry at once.
    /// </summary>
    public const int Slots = 2;

    readonly ShieldStats[] _fitted;

    /// <summary>
    /// Fills the shield slots.
    /// </summary>
    /// <param name="fitted">
    /// The shields to fit, at most <see cref="Slots"/> of them. Fewer is an ordinary answer — a
    /// ship early in the story has none at all.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="fitted"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// More shields than there are slots, or a slot holding nothing. An empty slot is expressed by
    /// fitting fewer shields, so a <c>null</c> among them is a caller that lost one rather than a
    /// caller leaving a slot free.
    /// </exception>
    public Shielding(params ShieldStats[] fitted)
    {
        ArgumentNullException.ThrowIfNull(fitted);

        if (fitted.Length > Slots)
        {
            throw new ArgumentException($"A ship has {Slots} shield slots.", nameof(fitted));
        }

        if (Array.Exists(fitted, shield => shield is null))
        {
            throw new ArgumentException("A shield slot holds a shield or nothing at all.", nameof(fitted));
        }

        // copied rather than kept, so a caller that goes on to reuse its array cannot change what a
        // ship is wearing after it has been fitted
        _fitted = [.. fitted];

        Capacity = _fitted.Sum(shield => shield.Capacity);

        // held at the whole hit: each shield is authored on its own and neither of a pair is a
        // mistake, but a pair adding past one would send back more damage than arrived
        ReflectedFraction = Math.Min(1, _fitted.Sum(shield => shield.ReflectedFraction));
    }

    /// <summary>
    /// Gets the empty slots a ship carries before it has found a shield.
    /// </summary>
    public static Shielding None { get; } = new();

    /// <summary>
    /// Gets the shields fitted, in the order they were slotted.
    /// </summary>
    public IReadOnlyList<ShieldStats> Fitted => _fitted;

    /// <summary>
    /// Gets the shield layer everything fitted adds up to — what stands in front of the hull.
    /// </summary>
    public double Capacity { get; }

    /// <summary>
    /// Gets the share of an incoming hit everything fitted sends back at the attacker, from 0 to 1.
    /// </summary>
    public double ReflectedFraction { get; }
}
