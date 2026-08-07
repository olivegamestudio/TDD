using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The Disgraced's ship: the one ship the game is flown in, and the numbers that decide how it
/// feels to fly it.
/// </summary>
/// <remarks>
/// <para>
/// The physics belongs to the engine; these numbers are game content and belong here. When the
/// player earns a better ship, that is another set of these, not another physics.
/// </para>
/// <para>
/// Named for the pilot rather than the game. Naming it after the game would say only which
/// product the type belongs to, which every type in this assembly already shares; the fiction is
/// more specific — "You are <b>the Disgraced</b>", flying one ship, critically low on everything.
/// </para>
/// <para>
/// Deliberately not derived from a <c>Ship</c> base type. A base class with a single derived type
/// is a hierarchy invented ahead of anything to factor out of, and the axis this game varies is
/// equipment rather than hulls. The second ship is what should decide the shape, and by then
/// there will be two sets of numbers to compare.
/// </para>
/// </remarks>
public static class DisgracedShip
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
    /// every correction feel like an argument with the controls. It shipped at 2.5, which is two
    /// and a half seconds to come about and not what this remark or the tests ever asked for; the
    /// duration is the number that was agreed, so the rate is the one that moved.
    /// </remarks>
    public static ShipHandling Handling { get; } = new(
        Acceleration: 180,
        Drag: 0.9,
        TurnRate: 4.5);

    /// <summary>
    /// Gets the hull as content authors it: how it flies, what it takes to destroy, and what wear
    /// it can stand.
    /// </summary>
    /// <remarks>
    /// <b>The health and durability numbers are placeholders and are not tuned.</b> Nothing damages
    /// a ship yet and nothing wears one out, so there is no play to tune them against; a hundred of
    /// each is a round number that reads as a full pool rather than a balance decision anybody has
    /// made. The handling beside them is not a placeholder — it is the tuning quest 1 was checked
    /// against.
    ///
    /// <b>Sixteen cargo slots is the design's indicative starter number and is not tuned either.</b>
    /// Nothing is bought, sold or crafted yet, so there is nothing to run out of room against; what
    /// the number does today is make the hold a real, countable size rather than an unbounded list.
    /// The bays that extend it are a later purchase and are not modelled.
    ///
    /// Nothing is fitted. The Disgraced starts with eight empty slots, which is what the design
    /// asks for — "critically low on everything" is a ship with the slots and nothing in them.
    ///
    /// <b>The hull radius is reasoned, not measured, and is not tuned either.</b> <see cref="ShipView.LengthInWorldUnits"/>
    /// says the ship is 30 units nose to engines, but that is the sprite's authored length, not its
    /// width — a square texture with a narrower silhouette drawn inside it, and nothing here reads
    /// pixels to find where the hull actually ends. Eight is a fraction of the length that reads as
    /// "narrower than it is long" without anything to check it against; the debris field is the
    /// first thing this hull collides with, and it is a number a human flying through one should
    /// settle rather than this comment.
    /// </remarks>
    public static ShipProfile Profile { get; } = new(
        Handling,
        Health: 100,
        Durability: 100,
        CargoSlots: 16,
        Loadout: [],
        HullRadius: 8);
}
