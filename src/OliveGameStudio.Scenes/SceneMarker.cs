namespace OliveGameStudio;

/// <summary>
/// Something in the scene that the game reacts to: a place where a quest begins, progress is
/// written, the player respawns, someone can be spoken to.
/// </summary>
/// <remarks>
/// <para>
/// A marker is data about a place, not behaviour at it. What a <see cref="Kind"/> means is the
/// game's business — the engine reads and writes markers without learning what a quest or a vendor
/// is, exactly as it stores save text without learning what a save contains.
/// </para>
/// <para>
/// <b>The reference is why this is not a naming convention.</b> A previous build named its zones
/// <c>Q1_START_ZONE</c> and recovered the quest by parsing that back out, which made a typo in a
/// name silently detach a zone from its quest, with nothing anywhere reporting it.
/// <see cref="Reference"/> says outright which thing is meant, and <see cref="Name"/> goes back to
/// being what a human calls it.
/// </para>
/// </remarks>
/// <param name="Name">
/// What the author called it. Diagnostics and the editor's own display; nothing is resolved by it.
/// </param>
/// <param name="Kind">
/// What sort of marker this is. An identifier the game defines and interprets — the engine only
/// carries it.
/// </param>
/// <param name="Reference">
/// Which thing of that kind is meant: the quest id a start zone begins, the vendor a shop marker
/// is. An identifier, never translated. Empty where the kind needs no target — a save zone is a
/// save zone, and there is nothing for it to point at.
/// </param>
/// <param name="X">Where it stands along the world's X axis.</param>
/// <param name="Y">Where it stands along the world's Y axis.</param>
/// <param name="RotationDegrees">Which way it faces, in degrees. Ignored by kinds that have no facing.</param>
/// <param name="Radius">
/// How close counts as arriving, in world units. This is the number a proximity trigger cannot do
/// without, and the one the previous build's export lost: its zones were circles in the editor and
/// bare points in the file, so the distance that had been authored existed nowhere afterwards.
/// Zero for a marker that is a position rather than an area.
/// </param>
public sealed record SceneMarker(
    string Name,
    string Kind,
    string Reference,
    double X,
    double Y,
    double RotationDegrees,
    double Radius);
