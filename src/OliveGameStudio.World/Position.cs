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
    /// <para>
    /// The journey's length is never formed, so there is no length for a long journey to overflow.
    /// The closest point is taken as a weighted blend of the two ends rather than as the start plus
    /// a share of the difference between them: a blend of two finite ends is finite whatever lies
    /// between them, whereas the difference alone is not, and a journey spanning the width of a
    /// <see langword="double"/> would otherwise put the closest point at infinity.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where it ended.</param>
    /// <returns>The distance in world units, never negative.</returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double along = FractionAlongSegment(from, to);
        double back = 1 - along;

        return DistanceTo(new Position(
            (back * from.X) + (along * to.X),
            (back * from.Y) + (along * to.Y)));
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
    /// <para>
    /// A fraction is a ratio of two quantities that both grow with the square of the journey, so
    /// the journey may be measured at any convenient size without changing the answer. It is
    /// measured here at half size and then in units of its own longer axis, which is what keeps
    /// every quantity formed along the way inside a <see langword="double"/>. Squaring the length
    /// directly overflowed above roughly 1.34e154 and then divided infinity by infinity, so a
    /// journey longer than that answered <see cref="double.NaN"/> or collapsed to 0 — and a
    /// collapsed fraction is a sweep silently reporting the ground it covered as a single point at
    /// the start. This holds at any magnitude, so it does not depend on how far out a position is
    /// allowed to be; that is a separate rule, kept at the save boundary rather than here.
    /// </para>
    /// <para>
    /// For three finite positions the answer is always a fraction and never
    /// <see cref="double.NaN"/>, which matters more than it looks: an ordered comparison against
    /// NaN is false whichever way round it is written, so a caller ordering two markers by when a
    /// journey reached them would be told neither came first and would quietly do nothing.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where it ended.</param>
    /// <returns>A fraction between 0 and 1, or <see cref="double.NaN"/> for a non-finite journey.</returns>
    public double FractionAlongSegment(Position from, Position to)
    {
        // Halved before subtracting, so that two ends at opposite extremes of a double's range
        // still have a difference that is a number. Halving is exact; subtracting first is not.
        double halfFromX = from.X * 0.5;
        double halfFromY = from.Y * 0.5;
        double dx = (to.X * 0.5) - halfFromX;
        double dy = (to.Y * 0.5) - halfFromY;

        // The journey's longer axis. Dividing by it leaves one of the two components at exactly
        // one and the other no larger, so the squares below land between 1 and 2 whatever the
        // journey's length — there is no length here for a long journey to overflow.
        double longerAxis = Math.Max(Math.Abs(dx), Math.Abs(dy));

        if (longerAxis is 0)
        {
            // began and ended in the same place
            return 0;
        }

        double alongX = dx / longerAxis;
        double alongY = dy / longerAxis;

        // Neither product can overflow, because neither component exceeds one in magnitude, so
        // this is a sum of two finite terms: it may reach infinity, but it cannot reach NaN.
        double reach = (((X * 0.5) - halfFromX) * alongX) + (((Y * 0.5) - halfFromY) * alongY);

        // Divided by the axis separately rather than folded into a single denominator, so that an
        // enormous reach and an enormous journey are never divided by one another. Whatever the
        // magnitudes, the sign survives, and the sign is what decides which end the clamp lands on.
        return Math.Clamp(reach / longerAxis / ((alongX * alongX) + (alongY * alongY)), 0, 1);
    }

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
