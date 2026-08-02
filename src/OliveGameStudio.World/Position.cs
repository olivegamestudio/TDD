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
    /// Measures the closest this position came to a journey from <paramref name="from"/> to
    /// <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// This is the measure a system that reacts to the player wants when the player is moving.
    /// <see cref="DistanceTo"/> answers "how far away is the player <em>now</em>", which quietly
    /// becomes "how far away was the player at the instant the frame happened to end" — and a
    /// frame is long enough to carry a ship clean through something it passed within metres of.
    /// This answers "how close did the player get", which is the question that does not depend on
    /// when the frames fell.
    ///
    /// It measures the segment, not the infinite line the segment lies on: a journey that stopped
    /// short is measured to where it stopped. That is what keeps this from widening the approach
    /// sideways — the closest approach to a journey is never nearer than the closest approach to
    /// the whole of it, so nothing that was far away becomes near.
    ///
    /// The journey is rescaled by its longer side before the fraction along it is worked out, so
    /// no quantity is ever squared at full magnitude. Squaring the length directly overflows a
    /// <see cref="double"/> somewhere above 1.3e154, and the failure is silent: the fraction
    /// collapses and the measure degrades to sampling one end while presenting itself as having
    /// swept. Two extra divides on a per-frame path buys the answer being right at any size.
    /// </remarks>
    /// <param name="from">Where the journey began.</param>
    /// <param name="to">Where the journey ended.</param>
    /// <returns>
    /// The distance in world units to the nearest point on the journey, never negative; the
    /// distance to <paramref name="from"/> when the journey went nowhere. It is
    /// <see cref="double.NaN"/> when the journey cannot be measured — an endpoint that is not a
    /// number, or two so far apart that the difference between them is not finite. Every ordered
    /// comparison against <see cref="double.NaN"/> is false, so a caller testing this against a
    /// tolerance fires nothing rather than firing everything.
    /// </returns>
    public double DistanceToSegment(Position from, Position to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;

        double scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (scale == 0)
        {
            // a frame in which the player did not move. The journey is a point, and there is no
            // direction along it to resolve — measuring it is the point measure.
            return DistanceTo(from);
        }

        // rescaled, so the squares below are of numbers in [-1, 1] and one of them is exactly ±1
        double unitX = dx / scale;
        double unitY = dy / scale;

        // how far along the journey the closest approach falls, clamped into it so a marker
        // behind the start or beyond the end is measured to the end it is behind or beyond
        double along = Math.Clamp(
            ((((X - from.X) / scale) * unitX) + (((Y - from.Y) / scale) * unitY))
                / ((unitX * unitX) + (unitY * unitY)),
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
