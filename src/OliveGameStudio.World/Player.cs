namespace OliveGameStudio;

/// <summary>
/// The player entity: where the player is in the world, and what they fly. How the ship gets
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
    /// Gets the ship the player flies, or <c>null</c> before they have been given one.
    /// </summary>
    /// <remarks>
    /// Nullable because a player exists before a game does. Nothing awards a ship until a new
    /// game starts or a save is resumed, and a player between those moments has genuinely not
    /// been given one yet — a placeholder ship would be a lie the rest of the code would have to
    /// keep checking for anyway.
    /// </remarks>
    public Ship? Ship { get; private set; }

    /// <summary>
    /// Gives the player a ship, replacing whatever they were flying.
    /// </summary>
    /// <remarks>
    /// Awarding rather than setting: this is how a new game hands the player their first hull and
    /// how a save puts them back in the one they had. It does not move the player, because
    /// changing ship is not travel.
    /// </remarks>
    /// <param name="ship">The ship the player now flies.</param>
    public void Award(Ship ship) => Ship = ship;

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
