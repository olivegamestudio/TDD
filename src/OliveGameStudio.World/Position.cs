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
    /// Measures the straight line distance between this position and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The position to measure to.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceTo(Position other) => Math.Sqrt(
        ((other.X - X) * (other.X - X)) + ((other.Y - Y) * (other.Y - Y)));

    /// <summary>
    /// Measures the straight line distance between this position and the nearest point on the
    /// journey a traveller made from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the sweeping companion to <see cref="DistanceTo"/>, and it exists because a frame
    /// is not a moment. Measuring against the position a frame happened to finish on asks "is the
    /// traveller near this now?", which misses anything they went straight past — the faster they
    /// travel, the more of the world a frame skips over. Measuring against the segment asks "did
    /// they come near this at any point on the way?", which is the question a trigger means.
    /// </para>
    /// <para>
    /// The measurement is to the <em>segment</em>, not the infinite line through it: the fraction
    /// along is clamped to <c>[0, 1]</c>, so a point beyond either end is measured to that end.
    /// Ground the traveller has not covered is not near them, however well aimed they were. That
    /// clamp is also what stops the sweep widening a trigger sideways — the result is never
    /// greater than the distance to either end, so sweeping can only bring a point closer.
    /// </para>
    /// <para>
    /// A journey that went nowhere is a point, and answers exactly as <see cref="DistanceTo"/>
    /// would. A journey with a non-finite end answers <see cref="double.NaN"/> rather than
    /// throwing; every comparison against that is false, so a caller testing a tolerance measures
    /// nothing as near, which is the safe direction to fail in.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the traveller set off from.</param>
    /// <param name="to">Where the traveller arrived.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared <= 0)
        {
            // a standstill: the journey is a point, so there is nothing to project onto
            return DistanceTo(from);
        }

        // how far along the journey the closest approach happened, as a fraction of its length
        double along = Math.Clamp(
            (((X - from.X) * dx) + ((Y - from.Y) * dy)) / lengthSquared,
            0,
            1);

        return DistanceTo(new Position(from.X + (along * dx), from.Y + (along * dy)));
    }

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
