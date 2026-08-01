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
    /// Measures the shortest distance between this position and the straight line the traveller
    /// covered going from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// This is what lets a trigger be measured against a whole frame rather than the instant it
    /// ended. Sampling the end of the frame asks "is the player near the marker now"; this asks
    /// "did the player come near the marker at any point on the way", which is the question a
    /// ship travelling at speed makes the difference between.
    ///
    /// The segment, not the line it lies on: a traveller heading towards a marker and stopping
    /// short of it has not reached it, so the nearest point is clamped to the ends.
    /// </remarks>
    /// <param name="from">Where the travel began.</param>
    /// <param name="to">Where it ended.</param>
    /// <returns>
    /// The distance in world units, never negative, and never more than the distance to either
    /// end. A traveller who never moved is a point, and measures as one.
    /// </returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared == 0)
        {
            // a frame that covered no ground; there is no direction to project along
            return DistanceTo(from);
        }

        // how far along the travel the nearest point lies, as a fraction of it, clamped into the
        // frame so that ground the traveller has not covered yet does not count as travelled
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
