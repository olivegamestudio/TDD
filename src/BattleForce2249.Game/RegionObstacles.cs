using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Turns a loaded region's solid scenery into obstacles the ship's physics can collide with.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SceneBody"/> carries a position and an authored scale, never a physical size —
/// nothing about how big a rock actually is exists until a texture is loaded, and this runs ahead
/// of that. So the size a body collides at is worked out the same way <see cref="RegionView"/>
/// works out the size it is <em>drawn</em> at: the sprite's real pixel dimensions, converted
/// through <see cref="RegionView.AuthoredPixelsPerUnit"/>. <see cref="RadiusPerUnitOfScale"/> is
/// that conversion, one entry per collidable sprite, and the pixel sizes behind it are measured
/// from the shipped textures rather than guessed — a rock authored twice as large collides twice
/// as large, and a sprite this has no entry for is refused rather than silently made harmless.
/// </para>
/// <para>
/// The radius is a circle averaging a rock's width and height, not its outline — every rock here
/// is drawn wider than it is tall, and a circle is the shape that can be wrong in either direction
/// rather than only in one. Good enough to be blocked by, which is what was asked for; a rock the
/// player can graze past its own corner is a later refinement, not a defect in this one.
/// </para>
/// </remarks>
public static class RegionObstacles
{
    /// <summary>
    /// How much collision radius, in world units, one unit of a body's authored scale is worth —
    /// per sprite, because two rocks the same scale are not the same size if their art is not.
    /// </summary>
    /// <remarks>
    /// Each entry is <c>(pixelWidth + pixelHeight) / (4 × AuthoredPixelsPerUnit)</c>: half the
    /// average of the texture's own width and height, converted from authored pixels to world
    /// units. The pixel sizes are the texture's actual opaque art, not its canvas — measured from
    /// the shipped PNGs' own alpha channel rather than chosen by eye. <c>rock1.png</c>,
    /// <c>rock2.png</c> and <c>rock3.png</c> have no transparent margin at all, so their canvas
    /// size and their visible size are the same number: 985×562, 977×586, 974×657.
    /// <c>asteroid1.png</c> does — a 512×512 canvas with the asteroid itself occupying 69..442 by
    /// 36..476 of it, 373×440 — and using the full canvas there before this was measured had every
    /// solid asteroid colliding several units out from where it is actually drawn. The *shape*
    /// every entry is reduced to is still a circle regardless: see the remarks on the type for why.
    /// </remarks>
    static readonly IReadOnlyDictionary<string, double> RadiusPerUnitOfScale =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["rock1"] = RadiusPerUnitScale(pixelWidth: 985, pixelHeight: 562),
            ["rock2"] = RadiusPerUnitScale(pixelWidth: 977, pixelHeight: 586),
            ["rock3"] = RadiusPerUnitScale(pixelWidth: 974, pixelHeight: 657),
            ["asteroid1"] = RadiusPerUnitScale(pixelWidth: 373, pixelHeight: 440),
        };

    static double RadiusPerUnitScale(double pixelWidth, double pixelHeight) =>
        (pixelWidth + pixelHeight) / (4 * RegionView.AuthoredPixelsPerUnit);

    /// <summary>
    /// Adds every solid body in the scene to the ship's physics as a static obstacle.
    /// </summary>
    /// <param name="scene">The region already loaded.</param>
    /// <param name="ship">The ship whose physics the obstacles are added to.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scene"/> or <paramref name="ship"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A body is marked <see cref="SceneBody.Solid"/> for a sprite <see cref="RadiusPerUnitOfScale"/>
    /// has no entry for. A rock nobody measured the size of would otherwise ship as scenery the
    /// ship silently flies straight through — the opposite of what marking it solid asked for, and
    /// a mistake worth failing loudly on rather than discovering by flying into it.
    /// </exception>
    public static void Seed(SceneDefinition scene, ShipMovement ship)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(ship);

        foreach (SceneBody body in scene.Bodies)
        {
            if (!body.Solid)
            {
                continue;
            }

            if (!RadiusPerUnitOfScale.TryGetValue(body.Sprite, out double perUnit))
            {
                throw new InvalidOperationException(
                    $"'{body.Name}' ({body.Sprite}) is marked solid but has no known collision size.");
            }

            double authoredScale = (body.ScaleX + body.ScaleY) / 2;

            ship.AddObstacle(new Position(body.X, body.Y), authoredScale * perUnit);
        }
    }
}
