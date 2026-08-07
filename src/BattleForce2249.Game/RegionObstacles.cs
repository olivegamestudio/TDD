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
/// through <see cref="RegionView.AuthoredPixelsPerUnit"/>. <see cref="SizeOf"/> is that
/// conversion, one entry per collidable sprite, and the pixel sizes behind it are measured from
/// the shipped textures rather than guessed — a rock authored twice as large collides twice as
/// large, and a sprite this has no entry for is refused rather than silently made harmless.
/// </para>
/// <para>
/// The obstacle is a rectangle, not a circle — most of what this collides with is not round, and a
/// circle averaging a rock's width and height reads badly on anything long and thin, reaching out
/// over open space well past where a wall is actually drawn, or falling short of a corner still
/// well within it. A rectangle turned to the body's own authored rotation is wrong by a margin
/// rather than by a shape; matching an irregular rock outline exactly is a later refinement, not a
/// defect in this one.
/// </para>
/// </remarks>
public static class RegionObstacles
{
    /// <summary>
    /// A sprite's real, visible pixel dimensions — the opaque art, not the canvas it sits on.
    /// </summary>
    public readonly record struct PixelSize(double Width, double Height);

    /// <summary>
    /// How large a body's collision rectangle is, in world units.
    /// </summary>
    public readonly record struct ObstacleSize(double Width, double Height);

    /// <summary>
    /// Every collidable sprite's real pixel dimensions, measured from the shipped PNGs' own alpha
    /// channel rather than chosen by eye. <c>rock1.png</c>, <c>rock2.png</c> and <c>rock3.png</c>
    /// have no transparent margin at all, so their canvas size and their visible size are the same
    /// number: 985×562, 977×586, 974×657. <c>asteroid1.png</c> and <c>asteroid2.png</c> do — both
    /// 512×512 canvases, with the asteroid itself occupying 69..442 by 36..476 (373×440) and
    /// 59..448 by 47..454 (389×407) of them respectively — and using the full canvas before this
    /// was measured had every solid asteroid colliding several units out from where it is actually
    /// drawn.
    /// </summary>
    static readonly IReadOnlyDictionary<string, PixelSize> PixelSizePerSprite =
        new Dictionary<string, PixelSize>(StringComparer.Ordinal)
        {
            ["rock1"] = new PixelSize(985, 562),
            ["rock2"] = new PixelSize(977, 586),
            ["rock3"] = new PixelSize(974, 657),
            ["asteroid1"] = new PixelSize(373, 440),
            ["asteroid2"] = new PixelSize(389, 407),
        };

    /// <summary>
    /// Works out the collision rectangle's size a solid body would be seeded at, without seeding
    /// it.
    /// </summary>
    /// <param name="body">The body to measure.</param>
    /// <returns>The rectangle's width and height, in world units, along its own unrotated axes.</returns>
    /// <exception cref="InvalidOperationException">
    /// The body's sprite has no known collision size — see <see cref="Seed"/> for why that refuses
    /// rather than answering with a guess.
    /// </exception>
    /// <remarks>
    /// <see cref="Seed"/> and anything drawing a collision shape to check one against the eye —
    /// <c>CollisionDebugView</c>, today — read the same number through this rather than each
    /// keeping their own copy of how a scale and a sprite become a size. Width scales with
    /// <see cref="SceneBody.ScaleX"/> and height with <see cref="SceneBody.ScaleY"/> independently,
    /// matching how <see cref="RegionView"/> draws the same body.
    /// </remarks>
    public static ObstacleSize SizeOf(SceneBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!PixelSizePerSprite.TryGetValue(body.Sprite, out PixelSize pixels))
        {
            throw new InvalidOperationException(
                $"'{body.Name}' ({body.Sprite}) is marked solid but has no known collision size.");
        }

        double unitsPerPixel = 1 / RegionView.AuthoredPixelsPerUnit;

        return new ObstacleSize(
            pixels.Width * unitsPerPixel * body.ScaleX,
            pixels.Height * unitsPerPixel * body.ScaleY);
    }

    /// <summary>
    /// Works out the collision rectangle's rotation a solid body would be seeded at, in
    /// <see cref="ShipMovement.AddObstacle"/>'s own convention.
    /// </summary>
    /// <remarks>
    /// Matches <see cref="RegionView"/>'s own conversion exactly: a body's <see cref="SceneBody.RotationDegrees"/>
    /// is authored counter-clockwise in degrees, and the world this draws obstacles into turns
    /// clockwise in radians — the same reason the sprite that sits on top of this obstacle is
    /// rotated by the same formula to line up with it.
    /// </remarks>
    public static double RotationOf(SceneBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return -body.RotationDegrees * Math.PI / 180;
    }

    /// <summary>
    /// Adds every solid body in the scene to the ship's physics as a static obstacle.
    /// </summary>
    /// <param name="scene">The region already loaded.</param>
    /// <param name="ship">The ship whose physics the obstacles are added to.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scene"/> or <paramref name="ship"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A body is marked <see cref="SceneBody.Solid"/> for a sprite with no known collision size. A
    /// rock nobody measured the size of would otherwise ship as scenery the ship silently flies
    /// straight through — the opposite of what marking it solid asked for, and a mistake worth
    /// failing loudly on rather than discovering by flying into it.
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

            ObstacleSize size = SizeOf(body);
            ship.AddObstacle(new Position(body.X, body.Y), size.Width, size.Height, RotationOf(body));
        }
    }
}
