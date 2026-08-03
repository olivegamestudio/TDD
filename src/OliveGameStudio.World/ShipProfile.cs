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
/// counted.
/// </param>
/// <param name="Durability">
/// How much wear the hull can take. Distinct from health: health is what a fight costs and
/// durability is what time and use cost.
/// </param>
/// <param name="Loadout">What the hull comes fitted with. Empty is a perfectly good answer.</param>
public sealed record ShipProfile(
    ShipHandling Handling,
    double Health,
    double Durability,
    IReadOnlyList<Item> Loadout);
