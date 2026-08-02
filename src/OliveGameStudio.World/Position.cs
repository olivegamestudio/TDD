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
    /// journey travelled from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// The difference between this and <see cref="DistanceTo"/> is the difference between asking
    /// how close a traveller came to something and asking how close they were when they stopped.
    /// A caller sampling only where a frame ended cannot tell a marker it flew past from one it
    /// never approached, and the faster the traveller the more ground each frame hides.
    /// <para>
    /// The measurement is to the <em>segment</em>, not to the infinite line through it: a point
    /// beyond either end is measured to that end, so sweeping brings a marker closer than the
    /// nearer endpoint but never brings a sideways one into range. Non-finite coordinates answer
    /// <see cref="double.NaN"/>, which compares false against any distance a caller tests.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where it ended.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double along = FractionAlongSegment(from, to);

        return DistanceTo(new Position(
            from.X + (along * (to.X - from.X)),
            from.Y + (along * (to.Y - from.Y))));
    }

    /// <summary>
    /// Measures how far along the journey from <paramref name="from"/> to <paramref name="to"/>
    /// the traveller came nearest to this position, as a fraction of the whole journey.
    /// </summary>
    /// <remarks>
    /// This is what tells two markers swept by the same journey apart: the smaller fraction was
    /// reached first. Without it a swept trigger knows the ground was covered but not the order it
    /// was covered in, and a quest can be finished by a traveller who reached its objective before
    /// they reached its beginning.
    /// <para>
    /// Clamped to the ends, so a position beyond either one answers 0 or 1 rather than running off
    /// the line. A journey that went nowhere answers 0 — it began and ended in the same place, and
    /// everything was reached at once.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where it ended.</param>
    /// <returns>A fraction between 0 and 1, or <see cref="double.NaN"/> for a non-finite journey.</returns>
    public double FractionAlongSegment(Position from, Position to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared is 0)
        {
            return 0;
        }

        return Math.Clamp((((X - from.X) * dx) + ((Y - from.Y) * dy)) / lengthSquared, 0, 1);
    }

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
