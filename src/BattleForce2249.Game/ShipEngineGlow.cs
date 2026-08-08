using System.Numerics;

namespace BattleForce2249;

/// <summary>
/// Which end of the ship an engine glow belongs to, and so which way the pilot has to be burning
/// for it to light.
/// </summary>
public enum EngineGroup
{
    /// <summary>
    /// A bow thruster: it fires when the ship is asked astern, pushing the nose back the way it
    /// came.
    /// </summary>
    Fore,

    /// <summary>
    /// A main engine: it fires when the ship is asked ahead, pushing from the stern the way every
    /// engine in the class does.
    /// </summary>
    Aft,
}

/// <summary>
/// One of the glows the ship's engines cast, placed against the ship rather than against the
/// world.
/// </summary>
/// <remarks>
/// <para>
/// These arrived as scenery. The region the game opens in carried six <c>glow</c> bodies named
/// FrontEngine and RearEngine, standing at the world origin because that is where the ship they
/// were drawn around happens to start. Standing still it looked right, which is the whole
/// difficulty: the fault only appears once the player flies, and what it looks like then is the
/// ship leaving its own engines burning at the spawn point.
/// </para>
/// <para>
/// A glow that belongs to the ship has to be positioned by the ship, so the numbers move out of
/// the scene file and into the ship's own frame. They are the numbers the content was authored
/// with, kept unchanged and in the conventions they were authored in, so what shipped can still
/// be read against what it came from — this fixes where the glow is drawn, and deliberately
/// decides nothing about how it looks.
/// </para>
/// <para>
/// It is a game type rather than an engine one, for the reason the ship's asset key is: where a
/// ship's engines sit is content, and the engine ships no content.
/// </para>
/// </remarks>
/// <param name="Offset">
/// Where it sits relative to the middle of the hull, in world units, in the ship's own frame: X to
/// starboard, Y forward. Turned with the heading before it is drawn, so it stays on the same part
/// of the ship however the ship is pointed.
/// </param>
/// <param name="RotationDegrees">
/// How far the glow is turned within that frame, in degrees, anticlockwise-positive — the
/// convention the scene content is authored in. Degrees rather than radians, and anticlockwise
/// rather than the screen's clockwise, because these are the authored numbers; the conversion
/// belongs at the draw, where <see cref="RegionView"/> also performs it.
/// </param>
/// <param name="ScaleAcross">
/// How much the sprite is stretched across the ship, against
/// <see cref="RegionView.AuthoredPixelsPerUnit"/> — the authored X scale.
/// </param>
/// <param name="ScaleAlong">
/// How much it is stretched along the ship — the authored Y scale. Held separately from
/// <paramref name="ScaleAcross"/> because that is what makes a round glow into a plume, and a
/// single averaged factor draws every engine as a blob.
/// </param>
/// <param name="Group">
/// Which end of the ship this glow belongs to, and so which way the pilot has to be burning for
/// it to light — see <see cref="EngineGroup"/>.
/// </param>
public readonly record struct ShipEngineGlow(
    Vector2 Offset,
    float RotationDegrees,
    float ScaleAcross,
    float ScaleAlong,
    EngineGroup Group);
