namespace OliveGameStudio;

/// <summary>
/// The player entity: where the player is in the world, and what they are flying. How the ship
/// gets there — control input and physics — is a separate concern that drives this through
/// <see cref="MoveTo"/> and <see cref="MoveBy"/>.
/// </summary>
public sealed class Player
{
    /// <summary>
    /// Gets the player's current position in the world.
    /// </summary>
    public Position Position { get; private set; } = Position.Origin;

    /// <summary>
    /// Gets the ship the player is flying, or <c>null</c> before one has been awarded.
    /// </summary>
    /// <remarks>
    /// Nullable because a player exists before a game does: a session is constructed, then starts
    /// or resumes a game, and only then is there a ship to fly. Nothing should be flying or drawing
    /// a player until the session reports itself ready, by which point this is set.
    /// </remarks>
    public Ship? Ship { get; private set; }

    /// <summary>
    /// Gives the player a ship to fly, replacing whatever they were flying before.
    /// </summary>
    /// <remarks>
    /// Awarding is not a move. A player handed a better ship keeps the position they were at, and
    /// a player resuming a save is placed and awarded independently, so neither ordering can
    /// quietly teleport them.
    /// </remarks>
    /// <param name="ship">The ship the player now flies.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="ship"/> is <c>null</c>. Flying nothing is not a state a player can be put
    /// into deliberately, so it is a defect in the caller — and left to reach
    /// <see cref="Ship"/> it would surface as a null reference on whatever draws the ship, a long
    /// way from the code that caused it.
    /// </exception>
    public void Award(Ship ship) => Ship = ship ?? throw new ArgumentNullException(nameof(ship));

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
