namespace OliveGameStudio;

/// <summary>
/// The player entity. It owns nothing but where the player is in the world; how the ship gets
/// there — control input and physics — is a separate concern that drives this through
/// <see cref="MoveTo"/> and <see cref="MoveBy"/>.
/// </summary>
public sealed class Player
{
    /// <summary>
    /// Gets the player's current position in the world.
    /// </summary>
    public Position Position { get; private set; } = Position.Origin;

    /// <summary>
    /// Places the player at an absolute position, used when a game starts or a save is resumed.
    /// </summary>
    /// <param name="position">The position to move to.</param>
    public void MoveTo(Position position) => Position = position;

    /// <summary>
    /// Moves the player by the given amount on each axis, the per-frame shape of movement.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis; positive is forward.</param>
    public void MoveBy(double dx, double dy) => Position = Position.Offset(dx, dy);
}
