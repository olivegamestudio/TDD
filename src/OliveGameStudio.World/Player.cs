namespace OliveGameStudio;

/// <summary>
/// The player entity: where the player is in the world, and which ship they are in. How the ship
/// gets there — control input and physics — is a separate concern that drives this through
/// <see cref="MoveTo"/> and <see cref="MoveBy"/>.
/// </summary>
/// <remarks>
/// The ship is held here rather than alongside the player because it is the player's, not the
/// screen's: it has to survive a save, and a later incarnation flies whatever the persistent
/// record says they own. The player owns the ship; the physics only borrows how it handles.
/// </remarks>
public sealed class Player
{
    /// <summary>
    /// Gets the player's current position in the world.
    /// </summary>
    public Position Position { get; private set; } = Position.Origin;

    /// <summary>
    /// Gets the ship the player is flying, or <c>null</c> before any game has awarded one.
    /// </summary>
    /// <remarks>
    /// Nullable because the engine ships no ships. A player exists from the moment a session is
    /// constructed, and the game is what decides which ship a new game hands over, so there is a
    /// real moment — before a game has been started or resumed — when the answer is "none yet".
    /// Anything that draws or flies the ship should already be waiting on the session being ready.
    /// </remarks>
    public ShipModel? Ship { get; private set; }

    /// <summary>
    /// Puts the player in a ship, whether that is the one a new game awards or the one a save says
    /// they were already flying.
    /// </summary>
    /// <param name="ship">The ship the player now owns.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ship"/> is <c>null</c>.</exception>
    public void GiveShip(ShipModel ship)
    {
        ArgumentNullException.ThrowIfNull(ship);

        Ship = ship;
    }

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
