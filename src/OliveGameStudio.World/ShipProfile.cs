namespace OliveGameStudio;

/// <summary>
/// The hull a character flies, as content authors it: how it handles, what it is built to take, and
/// what it comes fitted with.
/// </summary>
/// <remarks>
/// <para>
/// A profile is authored and never changes; <see cref="Ship"/> is the instance built from it, and
/// it is the instance that takes damage. Two characters flying the same profile fly two ships.
/// </para>
/// <para>
/// <see cref="Health"/> and <see cref="Durability"/> are here and the shield is not, because the
/// first two are the hull's own and the shield is whatever is fitted to it. A hull with nothing
/// equipped still has a health pool and a structural condition; it has no shield at all.
/// </para>
/// </remarks>
/// <param name="Handling">How the hull flies — the numbers <see cref="ShipMovement"/> is built on.</param>
/// <param name="Health">
/// How much damage the hull itself absorbs before it is destroyed, before anything fitted to it is
/// counted. Must be a non-negative, finite number.
/// </param>
/// <param name="Durability">
/// How much wear the hull can take, as a non-negative, finite number. Distinct from health: health
/// is what a fight costs and durability is what time and use cost.
/// </param>
/// <param name="CargoSlots">
/// How much the hull can carry, counted in inventory slots. A fighter carries less than a hauler
/// <em>because it is a fighter</em>, so this is a role stat rather than a rank: it says what the
/// ship is for, not how far the player has come.
/// </param>
/// <param name="Loadout">
/// What the hull comes fitted with, as items to be slotted into the ship's
/// <see cref="OliveGameStudio.Loadout"/>. Empty is a perfectly good answer, and it is the one every
/// hull gives today.
/// </param>
/// <param name="HullRadius">
/// How much of a circle the hull fills, for colliding with the world — not how big it is drawn,
/// which is a rendering concern this never reaches. Must be a positive, finite number: a hull with
/// no size collides with nothing, and the ship would fly through everything in the world unharmed.
/// </param>
public sealed record ShipProfile(
    ShipHandling Handling,
    double Health,
    double Durability,
    int CargoSlots,
    IReadOnlyList<Item> Loadout,
    double HullRadius)
{
    /// <summary>
    /// How much damage the hull itself absorbs before it is destroyed.
    /// </summary>
    /// <remarks>
    /// Validated here for the reason <see cref="HullRadius"/> is: <see cref="Meter"/> refuses the
    /// same values, but only once a <see cref="Ship"/> is built from the profile, and by then the
    /// complaint names <c>maximum</c> — a constructor parameter of a type the content author has
    /// never heard of — rather than the field they actually wrote.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The pool is negative, not a number, or infinite. A negative pool is empty before anything
    /// touches it, so the hull arrives destroyed; an infinite one can never be emptied, so it cannot
    /// be destroyed at all. Zero is allowed, exactly as <see cref="Meter"/> allows it.
    /// </exception>
    public double Health { get; } = ValidatedPool(Health, nameof(Health));

    /// <summary>
    /// How much wear the hull can take.
    /// </summary>
    /// <remarks>
    /// Validated on the same terms as <see cref="Health"/>, and for the same reason.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The pool is negative, not a number, or infinite — see <see cref="Health"/>.
    /// </exception>
    public double Durability { get; } = ValidatedPool(Durability, nameof(Durability));

    /// <summary>
    /// How much the hull can carry, counted in inventory slots.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The count is negative. Zero is allowed — a hull with no hold is a thing content may
    /// legitimately author — but a negative one is a hold whose free space is a debt, and every
    /// question asked of it ("is there room?", "how much is left?") comes back a nonsense the
    /// player is shown.
    /// </exception>
    public int CargoSlots { get; } = Validated(CargoSlots);

    /// <summary>
    /// What the hull comes fitted with, as items to be slotted into the ship's
    /// <see cref="OliveGameStudio.Loadout"/>.
    /// </summary>
    /// <remarks>
    /// Copied rather than kept, for the same reason <see cref="Orbs"/> and <see cref="Shielding"/>
    /// copy what they are fitted with: <see cref="IReadOnlyList{T}"/> stops the profile's readers
    /// mutating the loadout, but it does not stop whoever authored the list from carrying on
    /// changing it — and a profile that changes after it is authored is no longer authored.
    /// </remarks>
    public IReadOnlyList<Item> Loadout { get; } = [.. Loadout];

    /// <summary>
    /// How much of a circle the hull fills, for colliding with the world.
    /// </summary>
    /// <remarks>
    /// <see cref="ShipMovement"/> refuses the same values, but only once a <see cref="Ship"/> is
    /// built from the profile. Refusing them here is what puts the complaint next to the content
    /// that caused it: the exception names <c>HullRadius</c>, the field an author wrote, rather
    /// than the constructor parameter of a type they have never heard of.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The radius is not a positive, finite number. A hull with no size collides with nothing and
    /// flies through the world unharmed; one with an infinite size collides with everything,
    /// everywhere, at once.
    /// </exception>
    public double HullRadius { get; } = Validated(HullRadius);

    // Takes the name rather than hard-coding one, because two of the profile's fields are the same
    // pool measured against the same rule and only differ in what they are called.
    static double ValidatedPool(double pool, string name)
    {
        // Named before it is ranged, for the reason the hull radius is: every comparison against a
        // NaN is false, so ThrowIfNegative stops the standard NaN only because its sign bit happens
        // to be set, and one whose sign bit is clear walks straight past. Refusing every non-finite
        // value here also covers positive infinity, which no sign check would have caught.
        if (!double.IsFinite(pool))
        {
            throw new ArgumentOutOfRangeException(
                name,
                pool,
                "A hull's pools have to be finite numbers.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pool, name);

        return pool;
    }

    static int Validated(int cargoSlots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cargoSlots, nameof(cargoSlots));

        return cargoSlots;
    }

    static double Validated(double hullRadius)
    {
        // Named before it is ranged: every comparison against NaN is false, so a NaN walks straight
        // past ThrowIfNegativeOrZero and only surfaces as a ship that collides with nothing.
        if (!double.IsFinite(hullRadius))
        {
            throw new ArgumentOutOfRangeException(
                nameof(HullRadius),
                hullRadius,
                "A hull has to be a finite size to collide with the world.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hullRadius, nameof(HullRadius));

        return hullRadius;
    }
}
