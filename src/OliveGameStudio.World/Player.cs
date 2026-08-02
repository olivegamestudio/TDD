namespace OliveGameStudio;

/// <summary>
/// The player entity. It owns nothing but where the player is in the world; how the ship gets
/// there — control input and physics — is a separate concern that drives this through
/// <see cref="MoveTo"/> and <see cref="MoveBy"/>.
/// </summary>
public sealed class Player
{
    Position _journeyStart = Position.Origin;

    /// <summary>
    /// Gets the player's current position in the world.
    /// </summary>
    public Position Position { get; private set; } = Position.Origin;

    /// <summary>
    /// Places the player at an absolute position, used when a game starts or a save is resumed.
    /// </summary>
    /// <remarks>
    /// A placement ends the journey the player was on rather than extending it. Nobody flew from
    /// where they were to where they have been put, so anything reading the ground covered — a
    /// proximity trigger, most obviously — must not be handed a line across a thousand units the
    /// player was never on.
    /// </remarks>
    /// <param name="position">The position to move to.</param>
    public void MoveTo(Position position)
    {
        Position = position;
        _journeyStart = position;
    }

    /// <summary>
    /// Moves the player by the given amount on each axis, the per-frame shape of movement.
    /// </summary>
    /// <param name="dx">The distance to move along the X axis.</param>
    /// <param name="dy">The distance to move along the Y axis; positive is forward.</param>
    public void MoveBy(double dx, double dy) => Position = Position.Offset(dx, dy);

    /// <summary>
    /// Takes the journey the player has flown since this was last called, and begins a new one
    /// from where they are now.
    /// </summary>
    /// <remarks>
    /// The player keeps this, rather than each system that wants it keeping its own copy of where
    /// the player used to be, because the player is the only thing that knows the difference
    /// between having flown somewhere and having been put there — and that difference is the whole
    /// of the answer. It is taken rather than read so that the next journey begins where this one
    /// ended: no ground is covered twice, and none is missed between two frames.
    /// </remarks>
    /// <returns>
    /// Where the journey began and where it ended. Both are the player's current position when
    /// they have not moved, and when they were placed rather than flown.
    /// </returns>
    public (Position From, Position To) TakeJourney()
    {
        (Position From, Position To) journey = (_journeyStart, Position);
        _journeyStart = Position;

        return journey;
    }
}
