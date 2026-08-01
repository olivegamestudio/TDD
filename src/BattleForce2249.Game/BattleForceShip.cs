using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The Disgraced's ship: the one ship the game is flown in, and the numbers that decide how it
/// feels to fly it.
/// </summary>
/// <remarks>
/// The physics belongs to the engine; these numbers are game content and belong here. When the
/// player earns a better ship, that is another set of these, not another physics.
/// </remarks>
public static class BattleForceShip
{
    /// <summary>
    /// The identifier the ship is saved under.
    /// </summary>
    /// <remarks>
    /// Save games name the ship by this, so it must not change once a build has written it into a
    /// file — and unlike anything the player reads it is never translated, because a save written
    /// in one language has to load in another.
    /// </remarks>
    public const string Id = "disgraced";

    /// <summary>
    /// The key of the asset that represents the ship on screen — <c>ship1</c>.
    /// </summary>
    /// <remarks>
    /// An identifier naming an asset, not text, so it is the same in every language. It lives on
    /// the ship so that whatever draws the player reads it from the ship they were awarded rather
    /// than hard-coding one graphic at the draw site.
    /// </remarks>
    public const string AssetKey = "ship1";

    /// <summary>
    /// Gets how the starting ship handles.
    /// </summary>
    /// <remarks>
    /// 180 units per second per second against a drag of 0.9 settles at 200 units a second, and
    /// reaches most of that within a couple of seconds — quick enough to feel responsive in a
    /// collapsing debris field, slow enough that the field is something you fly through rather
    /// than past. Quest 1's exit marker is 1000 units forward, so getting clear is a run of
    /// roughly six seconds at full burn.
    ///
    /// The turn rate is 4.5 radians a second — a little under a second and a half for a full
    /// circle — because the ship keeps its momentum through a turn and a slower helm would make
    /// every correction feel like an argument with the controls. It was 2.5, which is 2.5 seconds
    /// to come about: slower than this comment said and slower than the test asserting the feel
    /// would allow. The number was the one of the three that was wrong, because the helm is chosen
    /// for how fast the ship comes about, and both of the other two asked for a fast one. Ask
    /// <see cref="ShipHandling.SecondsForAFullCircle"/> rather than reading it off the radians.
    /// </remarks>
    public static ShipHandling Handling { get; } = new(
        Acceleration: 180,
        Drag: 0.9,
        TurnRate: 4.5);

    /// <summary>
    /// Gets the ship itself: the thing a new game awards the player, and the thing a save game
    /// names.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="Handling"/> rather than from a second copy of the numbers, so the
    /// handling the game registers for the physics and the handling the awarded ship carries cannot
    /// drift apart into two ships that fly differently. Declared after <see cref="Handling"/>
    /// because static initialisers run in source order.
    /// </remarks>
    public static Ship Ship { get; } = new(Id, AssetKey, Handling);
}
