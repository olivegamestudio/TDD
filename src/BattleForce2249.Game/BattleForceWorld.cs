using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Where a quest's markers stand in the world. The quest itself declares how close counts; this
/// says what the distance is measured from.
/// </summary>
/// <param name="QuestId">The quest these markers belong to.</param>
/// <param name="Start">The marker whose proximity begins the quest.</param>
/// <param name="End">The marker whose proximity completes it.</param>
public sealed record QuestMarkers(string QuestId, Position Start, Position End);

/// <summary>
/// The physical facts of the game world: where a new game puts the player, and where things stand.
/// </summary>
public interface IWorld
{
    /// <summary>
    /// Gets the position a brand new game starts the player at.
    /// </summary>
    Position PlayerStart { get; }

    /// <summary>
    /// Gets the markers each quest's proximity triggers are measured from.
    /// </summary>
    IReadOnlyList<QuestMarkers> QuestMarkers { get; }
}

/// <summary>
/// The world of Battle Force 2249. Forward travel is along the positive Y axis.
/// </summary>
public sealed class BattleForceWorld : IWorld
{
    /// <summary>
    /// How far from the origin the world extends along either axis, in world units. A position
    /// outside it is not one this game put the player at, and <see cref="SaveGameSerializer"/>
    /// refuses a save that claims otherwise.
    /// </summary>
    /// <remarks>
    /// A billion units is a million times the furthest marker the campaign authors, so no amount
    /// of flying reaches it — at ten units a frame and sixty frames a second it is nineteen days
    /// of unbroken forward travel. It is deliberately generous, because the cost of drawing the
    /// line too close is a legitimate save refused and then overwritten by the new game that
    /// replaces it. It is bounded at all because the arithmetic the game runs on stops working
    /// long before <see cref="double"/> does: past about 1e15 a ten unit step no longer changes
    /// the position it is added to, so the ship would sit still with the engine reporting motion.
    /// </remarks>
    public const double Extent = 1e9;

    /// <inheritdoc />
    public Position PlayerStart => new(0, 0);

    /// <inheritdoc />
    public IReadOnlyList<QuestMarkers> QuestMarkers { get; } =
    [
        new QuestMarkers(
            BattleForceCampaign.Quest1Id,
            // on the player's starting position, so quest 1 begins on a new game launch
            Start: new Position(0, 0),
            // 1000 units forward, clear of the debris field
            End: new Position(0, 1000)),
    ];
}
