namespace Pilgrimage;

/// <summary>
/// A point in the world with a tolerance around it, used to decide whether the player has arrived
/// somewhere. Quests start and finish by distance from their markers rather than by an exact
/// position, so a player flying past at speed still triggers them.
/// </summary>
public readonly record struct QuestMarker
{
    /// <summary>
    /// Creates a marker at <paramref name="position"/> that is reached from anywhere within
    /// <paramref name="radius"/> world units of it.
    /// </summary>
    /// <param name="position">The centre of the marker.</param>
    /// <param name="radius">How close the player must get, in world units.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radius"/> is negative, which no position could ever satisfy.
    /// </exception>
    public QuestMarker(Position position, double radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        Position = position;
        Radius = radius;
    }

    /// <summary>
    /// Gets the centre of the marker.
    /// </summary>
    public Position Position { get; }

    /// <summary>
    /// Gets how close the player must get to the centre for the marker to be reached.
    /// </summary>
    public double Radius { get; }

    /// <summary>
    /// Determines whether a player standing at <paramref name="position"/> has reached this marker.
    /// </summary>
    /// <param name="position">The player's position.</param>
    /// <returns><c>true</c> when the position is within <see cref="Radius"/> of the marker.</returns>
    public bool IsReachedBy(Position position) => Position.DistanceTo(position) <= Radius;
}
