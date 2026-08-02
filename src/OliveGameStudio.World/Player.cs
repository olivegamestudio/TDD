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
    /// Gets where the player travelled from to reach <see cref="Position"/>, so a system that
    /// reacts to where they are can measure the ground they covered rather than sampling the point
    /// they stopped at. Equal to <see cref="Position"/> when they were placed rather than
    /// travelled, and when they have not moved at all.
    /// </summary>
    /// <remarks>
    /// The distinction between being placed and travelling is the reason this lives on the player
    /// rather than in whatever is doing the measuring. A remembered previous position would sweep a
    /// line the player never flew the moment a game is resumed or the player is put somewhere, and
    /// fire every trigger along it; <see cref="MoveTo"/> and <see cref="MoveBy"/> already know
    /// which of the two happened, so they are what answer it.
    /// </remarks>
    public Position TravelledFrom { get; private set; } = Position.Origin;

    /// <summary>
    /// Places the player at an absolute position, used when a game starts or a save is resumed.
    /// </summary>
    /// <remarks>
    /// A placement is not a journey: the player did not cross the ground in between, so
    /// <see cref="TravelledFrom"/> becomes the new position and nothing along the way is measured.
    /// </remarks>
    /// <param name="position">The position to move to.</param>
    public void MoveTo(Position position)
    {
        Position = position;
        TravelledFrom = position;
    }

    /// <summary>
    /// Moves the player by the given amount on each axis, the per-frame shape of movement.
    /// </summary>
    /// <remarks>
    /// Where the move started is kept as <see cref="TravelledFrom"/>, so the whole of the ground
    /// covered is measurable. That means a frame moving the player in more than one step has to
    /// combine those steps into one call — otherwise only the last of them is ground anything can
    /// see, which is the sampling this exists to end.
    /// </remarks>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis; positive is forward.</param>
    public void MoveBy(double dx, double dy)
    {
        TravelledFrom = Position;
        Position = Position.Offset(dx, dy);
    }
}
