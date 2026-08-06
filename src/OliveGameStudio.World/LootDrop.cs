namespace OliveGameStudio;

/// <summary>
/// One item lying in the world where it was dropped, and whether it has noticed the player yet.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of a drop that moves, which is why it is a class rather than a record while
/// the item it carries is a record: <see cref="Item"/> is what the thing <em>is</em> and never
/// changes, and this is where it currently happens to be. Two drops of the same item are two
/// separate things to fly over, so they are told apart by identity rather than by value.
/// </para>
/// <para>
/// It carries no drift speed and no reach of its own. Those are the magnet's — see
/// <see cref="LootMagnet"/> — because how loot reaches the player is one rule the player learns
/// once, rather than something each dropped item gets to answer differently.
/// </para>
/// <para>
/// Nothing here draws it. Where a drop is is a fact about the world; putting it on screen is
/// screen work of its own.
/// </para>
/// </remarks>
public sealed class LootDrop
{
    internal LootDrop(Item item, Position position)
    {
        Item = item;
        Position = position;
    }

    /// <summary>
    /// Gets the item that will be collected when this drop reaches the player.
    /// </summary>
    public Item Item { get; }

    /// <summary>
    /// Gets where the drop is now, in world units. Where it fell, until it is drawn in.
    /// </summary>
    public Position Position { get; private set; }

    /// <summary>
    /// Gets whether the player has come near enough for the drop to be travelling toward them.
    /// </summary>
    /// <remarks>
    /// <b>Once true this stays true</b>, even if the player flies straight back out of reach.
    /// Collection has no button, so a drop that gave up on the player is a drop they have no way
    /// of claiming; one that has been drawn in keeps coming and arrives when they slow down. This
    /// is a decision the model had to make to work at all rather than one the design states, and
    /// it is open to being overruled.
    /// </remarks>
    public bool Attracted { get; private set; }

    /// <summary>
    /// Notes that the player has come within the magnet's reach. Safe to call again; a drop is
    /// drawn in once and does not become more drawn in.
    /// </summary>
    internal void Attract() => Attracted = true;

    /// <summary>
    /// Moves the drop the given distance toward the player, and answers whether that reached them.
    /// </summary>
    /// <param name="player">Where the player is now the frame has ended.</param>
    /// <param name="step">How far the drop travels this frame, in world units.</param>
    /// <returns><c>true</c> when the drop arrived and is now the player's.</returns>
    internal bool DriftInto(Position player, double step)
    {
        double remaining = Position.DistanceTo(player);

        if (!double.IsFinite(remaining))
        {
            // a player nothing can be measured to. Scaling by that distance would put the drop
            // nowhere and leave it there, so one frame that could not be measured would lose the
            // item for good; it holds where it is instead and carries on when the next one can.
            return false;
        }

        if (remaining <= step)
        {
            // it would overshoot, so it arrives instead — a drop never sails past the player it
            // was coming to
            Position = player;

            return true;
        }

        // scaled by how much of what is left this frame covers, so the drop travels at the
        // magnet's speed rather than at a share of whatever distance it happens to be away
        double fraction = step / remaining;

        Position = Position.Offset(
            (player.X - Position.X) * fraction,
            (player.Y - Position.Y) * fraction);

        return false;
    }
}
