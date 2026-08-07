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
    /// <b>The hull radius is measured against the ship's own art, not reasoned by eye.</b> An
    /// earlier guess of 8 read as "narrower than the ship is long" with nothing to check it
    /// against, and was well under half the ship's real size — a ship that visibly overlapped a
    /// rock for several units before the hull it was flying on noticed. <c>ship1.png</c> is a
    /// 1024×1024 canvas, but the ship's own art — everything with any opacity — only fills
    /// 68..953 by 94..926 of it, 885×832 pixels. At <see cref="ShipView.LengthInWorldUnits"/>
    /// (30, mapped to the canvas' full height), that art is about 25.9 by 24.4 world units, and
    /// <see cref="HullRadius"/> is half the shorter of the two — a circle sized to fit inside the
    /// silhouette on its narrower axis rather than reach past either edge on its wider one. Still
    /// worth a play session before it is treated as settled; a circle is an approximation of a
    /// ship shaped nothing like one, and this only fixes how far wrong the approximation was.
    /// </remarks>
    public static ShipProfile Profile { get; } = new(
        Handling,
        Health: 100,
        Durability: 100,
        CargoSlots: 16,
        Loadout: [],
        HullRadius: HullRadius);

    /// <summary>
    /// Half the shorter axis of the ship's actual visible art, in world units — see the remarks
    /// on <see cref="Profile"/> for how the numbers behind this were measured.
    /// </summary>
    const double HullRadius = ShipView.LengthInWorldUnits * 832 / 1024 / 2;
}
