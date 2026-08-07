using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Draws the collision shapes nothing else on screen shows: a ring around the ship's hull, and one
/// around every solid body in the region, at the exact radius the physics actually collides at.
/// </summary>
/// <remarks>
/// <para>
/// A developer aid rather than a shipped feature, for the gap between "the physics is right" and
/// "the physics looks right": a hull or an obstacle sized wrong is invisible in the ordinary
/// render, because nothing else on screen is drawn at a collision radius rather than a sprite's
/// own size — a ship that visibly overlaps a rock for several units before anything registers a
/// hit looks the same as one that does not, right up until it is measured against something.
/// <see cref="Enabled"/> defaults to <c>true</c> while that gap is still being closed, and should
/// default to <c>false</c> once it is settled, not stay on as a permanent feature nobody asked for.
/// </para>
/// <para>
/// It reads <see cref="RegionObstacles.RadiusOf"/> for every solid body rather than keeping its own
/// copy of how a scale and a sprite become a size — the whole point is showing the number the
/// physics was actually seeded with, not a second guess at it that could quietly drift from the
/// first.
/// </para>
/// <para>
/// There is no primitive-shape drawing anywhere in this engine, so a ring is approximated as short
/// straight segments between points around a circle, each one a 1×1 pixel texture stretched to the
/// segment's length and rotated to its angle — the standard way to fake a line out of a sprite
/// batch. <c>pixel.png</c> is a single opaque white texel that exists for exactly this.
/// </para>
/// </remarks>
/// <param name="camera">The camera the world is drawn through — the same one everything else uses.</param>
public sealed class CollisionDebugView(ICamera camera) : IRenderable
{
    /// <summary>
    /// The asset key the line segments are stretched from: one opaque white pixel.
    /// </summary>
    public const string PixelAssetKey = "pixel";

    /// <summary>
    /// How many straight segments approximate one ring. Enough to read as a circle at the zoom
    /// this screen flies at without costing enough draws to matter for a developer-only overlay.
    /// </summary>
    const int Segments = 40;

    /// <summary>
    /// How thick a ring is drawn, in screen pixels — thick enough to find against a busy scene,
    /// thin enough that it still reads as an outline rather than a filled disc.
    /// </summary>
    const float LineThickness = 2f;

    static readonly Colour HullColour = new(0f, 1f, 0f, 1f);
    static readonly Colour ObstacleColour = new(1f, 0f, 0f, 1f);

    ITexture? _pixel;

    /// <summary>
    /// Gets or sets whether this draws at all. See the remarks on the type for why this defaults
    /// to <c>true</c> today and should not stay that way indefinitely.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets where the ship's hull ring is centred, in world units.
    /// </summary>
    public Vector2 ShipPosition { get; set; }

    /// <summary>
    /// Gets or sets the ship's hull radius, in world units — the same number
    /// <see cref="OliveGameStudio.ShipProfile.HullRadius"/> gives the physics.
    /// </summary>
    public double HullRadius { get; set; }

    /// <summary>
    /// Gets or sets the region's bodies to draw a ring around wherever
    /// <see cref="SceneBody.Solid"/> is set. Bodies that are not solid are read and skipped rather
    /// than expected to have been filtered already, so this can always be handed a region's whole
    /// <see cref="SceneDefinition.Bodies"/> list.
    /// </summary>
    public IReadOnlyList<SceneBody> Bodies { get; set; } = [];

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        if (!Enabled)
        {
            return;
        }

        ITexture pixel = _pixel ??= renderer.Textures.Load(PixelAssetKey);

        DrawRing(renderer, pixel, ShipPosition, HullRadius, HullColour);

        foreach (SceneBody body in Bodies)
        {
            if (!body.Solid)
            {
                continue;
            }

            DrawRing(
                renderer,
                pixel,
                new Vector2((float)body.X, (float)body.Y),
                RegionObstacles.RadiusOf(body),
                ObstacleColour);
        }
    }

    void DrawRing(IRenderer renderer, ITexture pixel, Vector2 worldCentre, double worldRadius, Colour colour)
    {
        Vector2 Point(int index)
        {
            float angle = MathF.Tau * index / Segments;
            Vector2 offset = new((float)worldRadius * MathF.Cos(angle), (float)worldRadius * MathF.Sin(angle));
            return camera.WorldToScreen(worldCentre + offset, renderer.ViewportSize);
        }

        Vector2 previous = Point(0);
        for (int segment = 1; segment <= Segments; segment++)
        {
            Vector2 next = Point(segment);
            DrawLine(renderer, pixel, previous, next, colour);
            previous = next;
        }
    }

    void DrawLine(IRenderer renderer, ITexture pixel, Vector2 from, Vector2 to, Colour colour)
    {
        Vector2 delta = to - from;
        float length = delta.Length();

        if (length <= 0f)
        {
            // two points that landed on the same pixel — a ring small enough on screen for this to
            // happen is one nobody could read anyway, and a zero-length stretch draws nothing
            return;
        }

        renderer.Draw(new Sprite(
            Texture: pixel,
            Position: from,
            Rotation: MathF.Atan2(delta.Y, delta.X),
            // the left edge of the pixel, vertically centred, so the stretched rectangle extends
            // from "from" towards "to" rather than growing out from its own middle
            Origin: new Vector2(0f, 0.5f),
            Scale: 1f)
        {
            Stretch = new Vector2(length, LineThickness),
            Colour = colour,
        });
    }
}
