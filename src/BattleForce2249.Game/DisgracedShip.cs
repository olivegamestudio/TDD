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
    /// The identifier the ship is saved under. Save games refer to it, so it must not change, and
    /// unlike anything the player reads it is never translated.
    /// </summary>
    public const string ShipId = "disgraced";

    /// <summary>
    /// The key of the graphic that stands for the ship. It is the name the art is built under in
    /// the content pipeline, not a file path and not text — a save or a screen written against it
    /// means the same thing in every language and on every platform.
    /// </summary>
    public const string GraphicKey = "ship1";

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
    /// The turn rate is 2.5 radians a second — a little under a second and a half for a full
    /// circle — because the ship keeps its momentum through a turn and a slower helm would make
    /// every correction feel like an argument with the controls.
    /// </remarks>
    public static ShipHandling Handling { get; } = new(
        Acceleration: 180,
        Drag: 0.9,
        TurnRate: 2.5);

    /// <summary>
    /// Gets the ship the player is awarded when a new game begins: the only ship the game has, and
    /// the one the fiction hands the Disgraced to fly out of the debris field in.
    /// </summary>
    /// <remarks>
    /// Declared after <see cref="Handling"/> on purpose. Static property initialisers run in the
    /// order they are written, so a ship built above the handling it is built from would be given
    /// a null one.
    /// </remarks>
    public static Ship Ship { get; } = new(ShipId, GraphicKey, Handling);
}
