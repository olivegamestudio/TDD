namespace OliveGameStudio;

/// <summary>
/// One authored region of the world: everything drawn in it, and everything that happens in it.
/// </summary>
/// <remarks>
/// <para>
/// A region is a <em>place</em> — the mines, a city, a debris field — hand-drawn rather than cut
/// from a grid, because the world is handcrafted and its seams should follow what was authored
/// rather than a lattice laid over the top. It is also the unit that streams: a load radius around
/// the player decides <em>when</em>, and this decides <em>what</em>.
/// </para>
/// <para>
/// <b>Positions are the world's, not the region's.</b> Every body and marker holds a world
/// coordinate, so two regions that meet do so without either converting anything, and a region can
/// be redrawn around its contents without moving them. It also keeps dungeons honest: entering one
/// teleports the player elsewhere in the same coordinate space rather than into a scene of its own,
/// so there is only ever one set of coordinates to reason about.
/// </para>
/// </remarks>
/// <param name="Id">
/// The region's stable identity, referred to by save games and by whatever sends the player here.
/// An identifier, never translated — a save written in one language has to load in another.
/// </param>
/// <param name="Bodies">The scenery, drawn in layer then order.</param>
/// <param name="Markers">The things the game reacts to.</param>
public sealed record SceneDefinition(
    string Id,
    IReadOnlyList<SceneBody> Bodies,
    IReadOnlyList<SceneMarker> Markers)
{
    /// <summary>
    /// An empty region. What a scene that could not be read comes back as, so that a missing or
    /// damaged file leaves the player somewhere empty rather than somewhere broken.
    /// </summary>
    public static SceneDefinition Empty { get; } = new(string.Empty, [], []);

    /// <summary>
    /// Whether two regions hold the same thing.
    /// </summary>
    /// <remarks>
    /// Written out rather than left to the compiler, because a record compares its members with
    /// <see cref="object.Equals(object?)"/> and a list's is reference equality — so a region that
    /// had just been written and read back would compare <em>unequal</em> to the one it came from,
    /// every time. A type that advertises value semantics and quietly does not have them is worse
    /// than one that never claimed them: the round trip is exactly what an editor asks about when
    /// it wants to know whether anything has changed since it last saved.
    /// </remarks>
    public bool Equals(SceneDefinition? other) =>
        other is not null
        && Id == other.Id
        && Bodies.SequenceEqual(other.Bodies)
        && Markers.SequenceEqual(other.Markers);

    /// <inheritdoc />
    /// <remarks>
    /// Built from the identity and the sizes rather than from every element. Equal regions produce
    /// equal hashes, which is the rule that matters, and hashing a region hundreds of entries long
    /// on every lookup would cost more than the collisions it avoids.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(Id, Bodies.Count, Markers.Count);
}
