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
/// The physical facts of the game world: where a new game puts the player, where things stand, and
/// where a ship entering the world is put down.
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

    /// <summary>
    /// Gets the people standing in the world, whether they stand still or walk a route.
    /// </summary>
    /// <remarks>
    /// The same list every frame, holding the same NPCs: a walking one carries where it has got to,
    /// so a world that answered with a fresh set each time would put everybody back where they
    /// started once a frame.
    /// </remarks>
    IReadOnlyList<Npc> Npcs { get; }

    /// <summary>
    /// Brings a ship into the world at a named place, and answers where that put it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The world is the placement authority.</b> Content says which place a character starts at;
    /// it does not say where that place is. Coordinates in content would make every place a number
    /// that has to be kept in step with the world by hand, and would leave a place with no identity
    /// beyond where it happens to be — which is the thing pillar 3 asks for.
    /// </para>
    /// <para>
    /// The ship is named because entering the world is something a ship does, and it is where a
    /// world that holds what is in it — traffic, hazards, other ships — will learn about this one.
    /// Answering where it stands is the whole of it today. It answers rather than places because
    /// position still lives on <see cref="Player"/> rather than on the ship; when the ship carries
    /// its own position this becomes a placement and the answer goes away.
    /// </para>
    /// </remarks>
    /// <param name="ship">The ship entering the world.</param>
    /// <param name="location">The identifier of the place it enters at.</param>
    /// <returns>Where in the world the ship now stands.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ship"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="location"/> is missing or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The world has no such place. That is a mistake in content rather than something a player did,
    /// so it fails where the content is read instead of stranding the player somewhere arbitrary.
    /// </exception>
    Position Introduce(Ship ship, string location);
}

/// <summary>
/// The world of Battle Force 2249. Forward travel is along the positive Y axis.
/// </summary>
public sealed class BattleForceWorld : IWorld
{
    /// <summary>
    /// The identifier of the mines the Disgraced starts in. An identifier rather than a coordinate,
    /// and never translated: a place named in a save written in one language has to be found again
    /// in another.
    /// </summary>
    public const string Mines = "mines";

    /// <inheritdoc />
    public Position PlayerStart => new(0, 0);

    /// <inheritdoc />
    /// <remarks>
    /// The mines are where the game opens, so they resolve to the same position a new game already
    /// started at — stated once here rather than twice, so the place and the start cannot drift
    /// apart.
    /// </remarks>
    public Position Introduce(Ship ship, string location)
    {
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        return location switch
        {
            Mines => PlayerStart,
            _ => throw new ArgumentOutOfRangeException(
                nameof(location),
                location,
                "The world has no place by that name."),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nobody yet. The machinery for talking to somebody is here and covered, but who is standing
    /// in the mines and what they say is <em>writing</em> — a character, a voice and a scene — and
    /// none of it has been written. An NPC invented to exercise the code would be fiction nobody
    /// asked for, sitting in the opening of the game where the design canon is most specific, so
    /// the tests author their own and the mines stay empty until somebody writes what is in them.
    /// </remarks>
    public IReadOnlyList<Npc> Npcs { get; } = [];

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
