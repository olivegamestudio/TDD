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
    /// Measures the shortest distance from this position to the straight line segment running from
    /// <paramref name="from"/> to <paramref name="to"/> — the ground something covered, rather than
    /// the point it finished on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what lets a test against a moving object be about the whole of a frame's travel. A
    /// point measured against another point only answers "was it near at that instant", so anything
    /// moving faster than the thing it is measured against can pass clean between two samples.
    /// </para>
    /// <para>
    /// The segment is a segment, not the infinite line through it: a point beyond either end is
    /// measured to that end. That is what keeps this from widening a test sideways — somewhere the
    /// traveller was heading for but never reached is still as far away as it ever was.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the travel began.</param>
    /// <param name="to">Where it ended. May be the same as <paramref name="from"/>.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared == 0)
        {
            // nothing moved, so the segment is a point and this is the ordinary distance
            return DistanceTo(from);
        }

        // how far along the segment the closest point lies, as a fraction of its length, clamped
        // to the ends so the measurement is against the segment and not the line through it
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
