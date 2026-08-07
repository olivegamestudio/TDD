namespace OliveGameStudio;

/// <summary>
/// Something a character can own and a ship can be fitted with: an identifier, and what that
/// identifier is worth to an inventory and a loadout.
/// </summary>
/// <remarks>
/// <para>
/// The identifier is an identifier and is never translated: an item named in a save written in one
/// language has to be found again in another. Whatever the player reads about an item will be
/// looked up from this, not stored in it.
/// </para>
/// <para>
/// <b>An item is its identifier, and only its identifier.</b> Two items with the same id are the
/// same item however they were authored, so an item read back from a save is the item the game
/// shipped rather than something that merely looks like it — which is what lets a save carry ids
/// and nothing else. <see cref="Stats"/> travels with the item because everything that holds one
/// needs it, but it does not decide which item this is: an id authored twice with two sets of
/// numbers is one item described wrongly, not two items, and <see cref="Inventory"/> is where that
/// is caught rather than somewhere it has quietly become a second stack.
/// </para>
/// <para>
/// What an item <em>does</em> is still not here. A shield's absorption is <see cref="ShieldStats"/>
/// and an orb's orbit is <see cref="OrbStats"/>; those are read by the systems that act on them,
/// and joining them to the item that carries them is the next step rather than this one.
/// </para>
/// </remarks>
/// <param name="Id">The stable identifier the item is known and saved by.</param>
/// <param name="Stats">How high it stacks and which slot it fits.</param>
public sealed record Item(string Id, ItemStats Stats)
{
    /// <summary>
    /// The stable identifier the item is known and saved by.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The identifier is missing or blank. An item nothing can name cannot be equipped, found in a
    /// save, or told apart from another one, so it is refused where it is built rather than
    /// wherever it is first looked for.
    /// </exception>
    public string Id { get; } = Validated(Id);

    /// <summary>
    /// How high the item stacks and which slot it fits.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The stats are <c>null</c>. Every holder of an item asks how high it stacks, so an item
    /// without an answer is one that cannot be picked up.
    /// </exception>
    public ItemStats Stats { get; } = Validated(Stats);

    /// <summary>
    /// Whether this is the same item as another — that is, whether they share an identifier.
    /// </summary>
    /// <remarks>
    /// Written out rather than left to the record, which would compare the stats too. That would
    /// make an item and the same item authored with a different stack limit two different items,
    /// and a save carrying ids could not rebuild what it saved without also having stored the
    /// numbers it was saved with.
    /// </remarks>
    /// <param name="other">The item to compare against.</param>
    /// <returns><c>true</c> when both name the same item.</returns>
    public bool Equals(Item? other) =>
        other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    static string Validated(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return id;
    }

    static ItemStats Validated(ItemStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return stats;
    }
}
