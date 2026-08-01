namespace OliveGameStudio;

/// <summary>
/// A point in the game world, in world units. Forward travel is along the positive Y axis.
/// </summary>
/// <param name="X">The position along the world's X axis.</param>
/// <param name="Y">The position along the world's Y axis.</param>
public readonly record struct Position(double X, double Y)
{
    /// <summary>
    /// The world origin, and the position a <see cref="Player"/> holds before a game is started.
    /// </summary>
    public static readonly Position Origin = new(0, 0);

    /// <summary>
    /// The furthest either coordinate may be from the origin and still be a place the player can
    /// play from.
    /// </summary>
    /// <remarks>
    /// Not an arbitrary fence, and not a limit on how far the game may be played: 2^52 units is
    /// where a <see cref="double"/>'s own steps grow as wide as a world unit. Below it a one unit
    /// move changes the position; above it <c>Offset(0, 1)</c> returns the position it started
    /// from, so the ship flies and never arrives, and no proximity trigger it is heading for can
    /// ever fire. A position past here is therefore not somewhere far away — it is nowhere, and
    /// the only honest thing to do with one is refuse it at the edge it arrives through.
    /// The game itself plays within a few thousand units of the origin, so this is many orders of
    /// magnitude clear of anything legitimate play produces.
    /// </remarks>
    public const double MaxCoordinate = 4503599627370496d;

    /// <summary>
    /// Gets whether this is a position the player can actually be put at and play on from.
    /// </summary>
    /// <remarks>
    /// Rejects the two ways a coordinate stops being a place. <c>NaN</c> and the infinities are
    /// not numbers a position can be measured from at all; anything beyond
    /// <see cref="MaxCoordinate"/> is a number the player can never move away from. Both leave the
    /// same symptom — a game that is running, and a quest that can never start or complete — so
    /// both are refused together rather than one being caught and the other admitted.
    /// </remarks>
    public bool IsWithinPlayableSpace =>
        double.IsFinite(X)
        && double.IsFinite(Y)
        && Math.Abs(X) <= MaxCoordinate
        && Math.Abs(Y) <= MaxCoordinate;

    /// <summary>
    /// Measures the straight line distance between this position and <paramref name="other"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="double.Hypot(double, double)"/> rather than squaring the gaps and rooting
    /// the sum, because the intermediate square overflows at a gap of 1.34e154 — the root of
    /// <see cref="double.MaxValue"/> — long before the distance itself would. Two real points
    /// reported <c>Infinity</c> between them, and a proximity check against <c>Infinity</c> does
    /// not fail loudly, it simply answers "not yet" forever. Hypot never forms the square, so the
    /// answer is a number wherever both points are.
    /// </remarks>
    /// <param name="other">The position to measure to.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceTo(Position other) => double.Hypot(other.X - X, other.Y - Y);

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
