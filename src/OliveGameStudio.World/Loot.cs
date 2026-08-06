namespace OliveGameStudio;

/// <summary>
/// What has been dropped into the world and not yet picked up, and the rule by which it reaches
/// the player.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no pickup button.</b> Coming near a drop is the whole of collecting it: the drop
/// notices the player, travels to them, and is theirs when it arrives. That keeps the mechanic
/// identical on a device with no buttons to spare, and it is why nothing here takes an input.
/// </para>
/// <para>
/// Reach is measured against the <em>journey</em> a frame covered rather than the point it ended
/// at — <see cref="Position.DistanceToSegment"/>, the same measure quest proximity is held to.
/// Sampling one point a frame would make a pickup a property of the frame rate: a frame long
/// enough to carry a ship clean through a drop collects nothing, and the faster the player flies
/// the more of their loot they leave behind. Pillar 1 of <c>docs/DESIGN.md</c> makes that the
/// model's problem rather than something content works around by dropping loot in wider piles.
/// </para>
/// <para>
/// It keeps no memory of where the player was, which is why the journey is passed in. Remembering
/// would draw a line across a resumed save, where the player is put down somewhere else rather
/// than flying there, and every drop lying between the two would be swept up on the first frame
/// back.
/// </para>
/// <para>
/// <b>What drops, and out of what, is not here.</b> This is the engine's half — the mechanic by
/// which a dropped thing reaches its owner. Which enemies and objects drop which items is content,
/// and nothing in this repository authors any yet.
/// </para>
/// </remarks>
public sealed class Loot
{
    readonly LootMagnet _magnet;
    readonly List<LootDrop> _drops = [];

    /// <summary>
    /// Creates an empty world, tuned by how loot in it reaches the player.
    /// </summary>
    /// <param name="magnet">How near a drop notices the player, and how fast it then travels.</param>
    /// <exception cref="ArgumentNullException"><paramref name="magnet"/> is <c>null</c>.</exception>
    public Loot(LootMagnet magnet)
    {
        ArgumentNullException.ThrowIfNull(magnet);

        _magnet = magnet;
    }

    /// <summary>
    /// Gets what is currently lying in the world, in the order it fell.
    /// </summary>
    /// <remarks>
    /// In the order it fell rather than sorted, so a screen drawing these draws the pile the way
    /// it accumulated rather than in an order nothing chose.
    /// </remarks>
    public IReadOnlyList<LootDrop> Drops => _drops;

    /// <summary>
    /// Drops an item into the world, to be collected when the player comes near it.
    /// </summary>
    /// <param name="item">The item dropped.</param>
    /// <param name="position">Where it fell, in world units.</param>
    /// <returns>The drop, so a caller that wants to follow this one can hold on to it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The position is not a pair of finite numbers. Every distance measured to such a drop is not
    /// a number, and every ordered comparison against that is false, so it would never be drawn in
    /// and never collected — an item lost silently the moment it fell. Refused where it is dropped
    /// rather than lying in the world being skipped by every frame.
    /// </exception>
    public LootDrop Drop(Item item, Position position)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "A drop has to fall somewhere the player could reach.");
        }

        LootDrop drop = new(item, position);
        _drops.Add(drop);

        return drop;
    }

    /// <summary>
    /// Hands an item straight over: a drop that cannot be missed, collected the instant it falls
    /// wherever the player happens to be.
    /// </summary>
    /// <remarks>
    /// This is the drop that never lies in the world at all, and it is deliberately a different
    /// method rather than a flag on <see cref="Drop"/>, because it does not share a single step
    /// with the ordinary path — there is nothing to reach, nothing to drift, and no frame at which
    /// it has not arrived. The game uses it where a missed drop would cost the player something
    /// they cannot go back for: an item a quest requires, dropped by the object they just
    /// destroyed. Proximity is generous but it is not a guarantee, and a quest that can be
    /// soft-locked by loot the player flew past is a bug the pickup rule should not be able to
    /// cause.
    /// </remarks>
    /// <param name="item">The item dropped.</param>
    /// <param name="into">The inventory it is collected into.</param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    public void DropGuaranteed(Item item, Inventory into)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(into);

        into.Add(item);
    }

    /// <summary>
    /// Draws in whatever the player's journey reached, moves what is already coming, and collects
    /// what arrives. Called once per frame, after the ship has flown.
    /// </summary>
    /// <remarks>
    /// A drop drawn in this frame drifts this frame too, so a player who stops on top of a pile
    /// does not wait a frame for it to start moving.
    /// </remarks>
    /// <param name="from">Where the player was when the frame began.</param>
    /// <param name="to">Where the player is now the frame has ended.</param>
    /// <param name="elapsedSeconds">How long the frame lasted, in seconds.</param>
    /// <param name="into">The inventory arriving drops are collected into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="into"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="elapsedSeconds"/> is negative or not a finite number. A frame of a length
    /// that is not a number moves every drawn drop nowhere and raises nothing while appearing to
    /// have run; time running backwards is not a shorter frame but a caller that subtracted the
    /// wrong way round.
    /// </exception>
    public void Update(Position from, Position to, double elapsedSeconds, Inventory into)
    {
        ArgumentNullException.ThrowIfNull(into);

        if (!double.IsFinite(elapsedSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "A frame has to be a finite number of seconds long.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(elapsedSeconds, nameof(elapsedSeconds));

        double step = _magnet.DriftSpeed * elapsedSeconds;

        // gathered rather than removed as they arrive, so the pile is walked in the order it fell
        // and what the player picked up reads that way in their inventory
        List<LootDrop>? collected = null;

        foreach (LootDrop drop in _drops)
        {
            if (!drop.Attracted && drop.Position.DistanceToSegment(from, to) <= _magnet.Reach)
            {
                drop.Attract();
            }

            if (drop.Attracted && drop.DriftInto(to, step))
            {
                collected ??= [];
                collected.Add(drop);
            }
        }

        if (collected is null)
        {
            return;
        }

        foreach (LootDrop drop in collected)
        {
            _drops.Remove(drop);
            into.Add(drop.Item);
        }
    }
}
