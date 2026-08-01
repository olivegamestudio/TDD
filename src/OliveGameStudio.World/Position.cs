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
    /// <returns>The distance in world units, never negative, and finite for any two finite points.</returns>
    /// <remarks>
    /// <see cref="double.Hypot(double, double)"/> rather than squaring both gaps and rooting the
    /// sum, because the squares overflow long before the answer does: a gap wider than the root of
    /// <see cref="double.MaxValue"/> — 1.34e154 — squared to Infinity, so two real points a real
    /// distance apart measured Infinity. Nothing throws on that. Every proximity test against it
    /// silently answers "not close", which is a quest that can never start and never complete.
    /// </remarks>
    public double DistanceTo(Position other) => double.Hypot(other.X - X, other.Y - Y);

    /// <summary>
    /// Produces the position reached by moving from here by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis.</param>
    /// <returns>The offset position; this instance is unchanged.</returns>
    public Position Offset(double dx, double dy) => new(X + dx, Y + dy);
}
