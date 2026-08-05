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

        // A new hull comes out of the yard with empty shield slots, so the layer is zero until
        // something is fitted. A ship with no shield and a ship whose shield is down read the same
        // here on purpose: both are one hit from the hull.
        Shielding = Shielding.None;
        Shield = new Meter(Shielding.Capacity);

        // and the additional slots come out of the yard empty for the same reason: an orb is
        // collected, and nothing has been collected yet
        Orbs = Orbs.None;

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
    /// <remarks>
    /// The shield slots are <see cref="Shielding"/> rather than part of this, and they hold stats
    /// rather than items. An <see cref="Item"/> is still only an identifier — what turns one into
    /// the numbers it is worth is the item model, which is open. Fitting shields by their stats is
    /// what lets the shields work now without inventing that answer early; when it arrives, reading
    /// a shield item's stats is what <see cref="Fit"/> is handed.
    /// </remarks>
    public Inventory Loadout { get; }

    /// <summary>
    /// Gets the hull's own health, and what is left of it.
    /// </summary>
    public Meter Health { get; }

    /// <summary>
    /// Gets what is in the ship's shield slots.
    /// </summary>
    public Shielding Shielding { get; private set; }

    /// <summary>
    /// Gets the shield layer the ship is carrying, and what is left of it. It comes from what is
    /// fitted, so a ship with empty shield slots has a maximum of zero.
    /// </summary>
    public Meter Shield { get; private set; }

    /// <summary>
    /// Gets what is in the ship's two additional slots — the companion objects that fly themselves.
    /// </summary>
    /// <remarks>
    /// They are here rather than in <see cref="Loadout"/> and they hold stats rather than items, for
    /// the reason <see cref="Shielding"/> is: an <see cref="Item"/> is still only an identifier, and
    /// what turns one into the numbers it is worth is the item model, which is open.
    /// </remarks>
    public Orbs Orbs { get; private set; }

    /// <summary>
    /// Gets the hull's structural condition, and what is left of it.
    /// </summary>
    public Meter Durability { get; }

    /// <summary>
    /// Gets the physics that flies this ship, built from its own <see cref="Handling"/>.
    /// </summary>
    public ShipMovement Movement { get; }

    /// <summary>
    /// Puts shields in the ship's shield slots, replacing whatever was in them.
    /// </summary>
    /// <remarks>
    /// The layer belongs to what is fitted, so refitting builds a new one at full strength rather
    /// than carrying over what the last set of shields had left. A shield that has just been bolted
    /// on is a shield that has not been shot at; keeping the old charge would mean a ship's
    /// remaining shield depended on what it used to be wearing, which nothing else in the model
    /// would explain to the player.
    /// </remarks>
    /// <param name="shielding">
    /// The shields to wear. <see cref="OliveGameStudio.Shielding.None"/> strips the slots.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="shielding"/> is <c>null</c>.</exception>
    public void Fit(Shielding shielding)
    {
        ArgumentNullException.ThrowIfNull(shielding);

        Shielding = shielding;
        Shield = new Meter(shielding.Capacity);
    }

    /// <summary>
    /// Puts orbs in the ship's additional slots, replacing whatever was in them.
    /// </summary>
    /// <remarks>
    /// There is no layer to rebuild the way <see cref="Fit(Shielding)"/> has to, because an orb
    /// carries nothing that depletes: what is fitted is the whole of what an orb is, and where it
    /// happens to be is worked out rather than remembered.
    /// </remarks>
    /// <param name="orbs">
    /// The orbs to carry. <see cref="OliveGameStudio.Orbs.None"/> strips the slots.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="orbs"/> is <c>null</c>.</exception>
    public void Fit(Orbs orbs)
    {
        ArgumentNullException.ThrowIfNull(orbs);

        Orbs = orbs;
    }

    /// <summary>
    /// Takes a hit: sends back what the shields reflect, soaks what the layer can hold, and puts the
    /// rest through to the hull.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolved in the order the fiction implies. What is reflected never arrives, so it is taken
    /// off the hit before anything else sees it — otherwise "bounces damage back" would mean the
    /// hull pays in full <em>and</em> the attacker is hurt too, which is a different shield from the
    /// one that was designed. What does arrive meets the shield layer first and bleeds into health
    /// only once the layer is gone.
    /// </para>
    /// <para>
    /// Nothing here decides what an empty meter means. A ship at zero health is destroyed, but what
    /// that costs the player is a rule about the game rather than about the hull, so it belongs
    /// where the fight does.
    /// </para>
    /// </remarks>
    /// <param name="amount">How much damage arrived. Zero is allowed and costs nothing.</param>
    /// <returns>Where the hit went.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative or not a number, for the reason <see cref="Meter.Reduce"/> refuses
    /// one: a damage calculation that came out backwards would otherwise heal what it was aimed at.
    /// </exception>
    public DamageOutcome TakeDamage(double amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (double.IsNaN(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A hit has to be a number.");
        }

        double reflected = amount * Shielding.ReflectedFraction;
        double landed = amount - reflected;

        double absorbed = Math.Min(landed, Shield.Current);
        Shield.Reduce(absorbed);

        double dealt = landed - absorbed;
        Health.Reduce(dealt);

        return new DamageOutcome(reflected, absorbed, dealt);
    }
}
