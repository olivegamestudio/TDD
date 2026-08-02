namespace OliveGameStudio;

/// <summary>
/// How fast something is moving and in which direction, in world units per second.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Position"/>. The two are measured in different units — units and
/// units per second — and one type that means both invites adding a position to a velocity and
/// getting a number back that means nothing.
/// </remarks>
/// <param name="X">The rate of travel along the world's X axis, in units per second.</param>
/// <param name="Y">The rate of travel along the world's Y axis, in units per second.</param>
public readonly record struct Velocity(double X, double Y)
{
    /// <summary>
    /// Not moving. The velocity a ship holds before it is flown, and the one it returns to.
    /// </summary>
    public static readonly Velocity Stationary = new(0, 0);

    /// <summary>
    /// Gets how fast this is, ignoring direction, in world units per second.
    /// </summary>
    public double Speed => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>
    /// Produces the velocity reached by changing this one by the given amount on each axis.
    /// </summary>
    /// <param name="dx">The change along the X axis, in units per second.</param>
    /// <param name="dy">The change along the Y axis, in units per second.</param>
    /// <returns>The changed velocity; this instance is unchanged.</returns>
    public Velocity Plus(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>
    /// Produces this velocity multiplied by a factor, keeping its direction.
    /// </summary>
    /// <param name="factor">The factor to multiply both axes by.</param>
    /// <returns>The scaled velocity; this instance is unchanged.</returns>
    public Velocity Scaled(double factor) => new(X * factor, Y * factor);
}
