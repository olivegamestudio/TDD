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
    /// Finds where along a journey a traveller first came within <paramref name="distance"/> of
    /// this position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A journey is measured as the straight segment between its ends, not as the point it
    /// finished at. Measured at the end of a frame, a trigger fires only if the frame happens to
    /// <em>land</em> inside it, so a long enough frame steps clean over a marker it passed within
    /// metres of; measured against the segment it cannot, however long the frame is.
    /// </para>
    /// <para>
    /// The answer is a fraction along the journey rather than a yes or no, because two triggers on
    /// one journey have an order between them: a traveller who passed the far marker before the
    /// near one flew the ground backwards, and something measuring both needs to be able to tell.
    /// </para>
    /// <para>
    /// The segment is closed at both ends, so this never widens a trigger sideways: a position
    /// beyond either end of the journey is measured to that end, and heading towards somewhere
    /// without reaching it is not arriving at it.
    /// </para>
    /// <para>
    /// A journey that cannot be measured — an end that is not a place, or one so far off that the
    /// arithmetic overflows — falls back to the point it began at, because that is the only end of
    /// it the traveller is known to have really been at. It is not silently treated as a journey
    /// to nowhere, which would drop a trigger the traveller was standing on.
    /// </para>
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where the journey ended.</param>
    /// <param name="distance">How close counts, in world units.</param>
    /// <returns>
    /// The fraction along the journey — 0 at <paramref name="from"/>, 1 at <paramref name="to"/> —
    /// at which the traveller first came within <paramref name="distance"/>, or
    /// <see langword="null"/> if they never did.
    /// </returns>
    public double? FirstApproachWithin(Position from, Position to, double distance)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double fx = from.X - X;
        double fy = from.Y - Y;

        // |from + t(to - from) - this|² = distance², as at² + bt + c = 0
        double a = (dx * dx) + (dy * dy);
        double b = 2 * ((fx * dx) + (fy * dy));
        double c = (fx * fx) + (fy * fy) - (distance * distance);

        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c))
        {
            // the journey is not measurable; measure where it began, if that was anywhere
            double atTheStart = DistanceTo(from);
            return double.IsFinite(atTheStart) && atTheStart <= distance ? 0 : null;
        }

        if (a == 0)
        {
            // a journey that covered no ground is the point it stood at
            return c <= 0 ? 0 : null;
        }

        double discriminant = (b * b) - (4 * a * c);
        if (discriminant < 0)
        {
            // the line the journey lies on never comes close enough, so neither does the journey
            return null;
        }

        // the traveller is within the distance between these two fractions, entering at the first
        double root = Math.Sqrt(discriminant);
        double entry = (-b - root) / (2 * a);
        double exit = (-b + root) / (2 * a);

        if (entry > 1 || exit < 0)
        {
            // close enough only before the journey began or after it ended
            return null;
        }

        // already within at the outset counts as arriving at the outset, not before it
        return Math.Max(entry, 0);
    }

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
