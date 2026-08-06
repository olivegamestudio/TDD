namespace OliveGameStudio;

/// <summary>
/// One piece of scenery: a sprite placed in the world, and nothing more.
/// </summary>
/// <remarks>
/// <para>
/// A body has no behaviour and nothing refers to it. It is drawn, and that is all it does — a rock,
/// a glow, a sheet of torn hull plating. Anything that has to be <em>reacted</em> to is a
/// <see cref="SceneMarker"/> instead, and keeping the two apart is what lets a scene be drawn
/// without anything being simulated, and simulated without everything being drawn.
/// </para>
/// <para>
/// <see cref="Sprite"/> is an asset key, not text. It is never translated, for the reason every
/// other identifier here is not: a scene authored in one language has to load in another.
/// </para>
/// </remarks>
/// <param name="Name">
/// What the author called it. For their benefit and for diagnostics only — nothing resolves a body
/// by name, and two bodies may share one.
/// </param>
/// <param name="Sprite">The asset key to draw.</param>
/// <param name="X">Where it stands along the world's X axis.</param>
/// <param name="Y">Where it stands along the world's Y axis.</param>
/// <param name="RotationDegrees">
/// How far it is turned, in degrees. Degrees rather than radians because this is authored by a
/// person in a tool, and "thirty degrees" is a number someone can type and check.
/// </param>
/// <param name="ScaleX">How much it is stretched across.</param>
/// <param name="ScaleY">
/// How much it is stretched up. Held separately from <paramref name="ScaleX"/> because scenery is
/// routinely squashed to make one asset serve several shapes — a glow pulled long is a different
/// object to the eye and the same asset to the content build.
/// </param>
/// <param name="Layer">
/// Which band of the scene it belongs to. Bodies are drawn layer by layer, so this is what puts a
/// planet behind a rock without either knowing about the other.
/// </param>
/// <param name="Order">
/// Where it sits within its layer, lowest first. Ties are drawn in the order they were authored,
/// so a scene that never sets this still draws the same way twice.
/// </param>
/// <param name="Parallax">
/// How much of the camera's movement this body takes: <c>1</c> is fixed in the world and slides
/// past at the speed the ship is flying, <c>0</c> is fixed to the screen and does not move at all,
/// and anything between lags by its share.
/// </param>
public sealed record SceneBody(
    string Name,
    string Sprite,
    double X,
    double Y,
    double RotationDegrees,
    double ScaleX,
    double ScaleY,
    string Layer,
    int Order,
    double Parallax = 1);
