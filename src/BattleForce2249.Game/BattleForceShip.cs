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
}
