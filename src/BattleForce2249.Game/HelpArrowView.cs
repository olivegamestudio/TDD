using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Draws the arrows that guide a new pilot out of the debris field, and takes each one down as
/// the ship gets close enough to have already been shown where to go.
/// </summary>
/// <remarks>
/// <para>
/// These are <see cref="SceneBody"/> content on the <c>"UI"</c> layer — see
/// <see cref="RegionView"/>'s own remarks on why that layer is skipped there rather than drawn
/// twice. Everything else about a body is read the same way <see cref="RegionView"/> reads it:
/// its own position and rotation, and its authored scale converted through the same
/// <see cref="RegionView.AuthoredPixelsPerUnit"/> the rest of the scenery is — an arrow authored
/// twice as large is drawn twice as large here too.
/// </para>
/// <para>
/// <b>Once an arrow has been reached, it stays gone — the fade does not undo itself.</b> A first
/// pass computed the fade fresh from distance every frame, so a player who doubled back past an
/// arrow saw it reappear exactly as it read on the way out. A play session called that out: the
/// point of taking an arrow down is that it has done its job, and finding it again on a return
/// trip through the same stretch reads as the guidance never having registered rather than as
/// anything the player earned by flying further. <see cref="Reset"/> is the one door this state
/// opens through, called by whatever starts a fresh flight — a resumed or restarted game is a new
/// approach to the same field, not a continuation of one already flown.
/// </para>
/// </remarks>
/// <param name="camera">The camera the world is drawn through — the same one everything else uses.</param>
public sealed class HelpArrowView(ICamera camera) : IRenderable
{
    /// <summary>
    /// The layer <see cref="Bodies"/> is filtered to. The same string <see cref="RegionView"/>
    /// excludes for the same reason: an arrow is drawn once, and this is where.
    /// </summary>
    public const string Layer = "UI";

    /// <summary>
    /// Beyond this distance from the ship, in world units, an arrow is drawn at full strength.
    /// </summary>
    /// <remarks>
    /// Untuned, and said plainly: reasoned against the spacing the arrows were authored at —
    /// roughly 50 to 110 units apart along the path out — rather than measured by flying past
    /// one, which is a judgement only a human at a screen can settle.
    /// </remarks>
    public const double FullyVisibleDistance = 60;

    /// <summary>
    /// Within this distance from the ship, in world units, an arrow is not drawn at all — it has
    /// done its job of pointing the way to here.
    /// </summary>
    /// <remarks>
    /// Reasoned the same way as <see cref="FullyVisibleDistance"/>, against the ship's own hull
    /// (see <see cref="OliveGameStudio.ShipProfile.HullRadius"/>) rather than measured: close
    /// enough that the ship is effectively on top of the arrow, not merely near it.
    /// </remarks>
    public const double HiddenDistance = 20;

    readonly Dictionary<string, ITexture> _textures = new(StringComparer.Ordinal);

    /// <summary>
    /// Every arrow the ship has already come within <see cref="HiddenDistance"/> of, at any point
    /// since the last <see cref="Reset"/> — kept by value, since a <see cref="SceneBody"/> record
    /// with the same fields <em>is</em> the same arrow as far as this is concerned. Reaching one
    /// once is permanent for the rest of the flight; only <see cref="Reset"/> clears it.
    /// </summary>
    readonly HashSet<SceneBody> _reached = [];

    /// <summary>
    /// Gets or sets where the ship is, so an arrow can be faded by how close it already is rather
    /// than drawn at a fixed strength regardless of where the player has actually flown to.
    /// </summary>
    public Vector2 ShipPosition { get; set; }

    /// <summary>
    /// Gets or sets the region's bodies to draw arrows from. Bodies outside <see cref="Layer"/>
    /// are read and skipped rather than expected to have been filtered already, so this can
    /// always be handed a region's whole <see cref="SceneDefinition.Bodies"/> list.
    /// </summary>
    public IReadOnlyList<SceneBody> Bodies { get; set; } = [];

    /// <summary>
    /// Forgets every arrow reached so far, so a fresh flight starts with all of them showing
    /// again. Called on entering the game screen, the same moment <c>GameScreen</c> clears its
    /// own per-flight state — a resumed or restarted game is a new approach to the field, not a
    /// continuation of the one that last reached these arrows.
    /// </summary>
    public void Reset() => _reached.Clear();

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        foreach (SceneBody body in Bodies)
        {
            if (!string.Equals(body.Layer, Layer, StringComparison.Ordinal))
            {
                continue;
            }

            if (_reached.Contains(body))
            {
                // Already earned its fade on some earlier frame — a return trip does not restore
                // it, and there is nothing left to compute.
                continue;
            }

            Vector2 world = new((float)body.X, (float)body.Y);
            float distance = Vector2.Distance(ShipPosition, world);

            if (distance <= HiddenDistance)
            {
                // Reached for the first time this frame: remembered from here on, and this frame
                // draws nothing for it either — the same outcome a later frame would compute, done
                // once rather than every frame from now on.
                _reached.Add(body);
                continue;
            }

            ITexture texture = TextureFor(renderer, body.Sprite);

            // One scale, from the average of the two the content carries — the same simplification
            // RegionView makes, for the same reason: a Sprite draws with a single factor.
            float authored = (float)((body.ScaleX + body.ScaleY) / 2);

            renderer.Draw(new Sprite(
                Texture: texture,
                Position: camera.WorldToScreen(world, renderer.ViewportSize),

                // Negated for the reason RegionView's is: the content is authored anticlockwise
                // with Y up, and a Sprite turns clockwise.
                Rotation: camera.WorldToScreenRotation((float)(-body.RotationDegrees * Math.PI / 180)),
                Origin: new Vector2(texture.Width, texture.Height) / 2f,
                Scale: authored * camera.PixelsPerUnit / RegionView.AuthoredPixelsPerUnit)
            {
                Colour = Colour.White.WithAlpha(AlphaAt(distance)),
            });
        }
    }

    /// <summary>
    /// How much of an arrow shows at a given distance from the ship: full strength beyond
    /// <see cref="FullyVisibleDistance"/>, gone within <see cref="HiddenDistance"/>, and a
    /// straight-line fade between the two.
    /// </summary>
    static float AlphaAt(float distanceFromShip)
    {
        if (distanceFromShip >= FullyVisibleDistance)
        {
            return 1f;
        }

        if (distanceFromShip <= HiddenDistance)
        {
            return 0f;
        }

        return (float)((distanceFromShip - HiddenDistance) / (FullyVisibleDistance - HiddenDistance));
    }

    /// <summary>
    /// The texture for an asset key, loaded once and kept — the same reason
    /// <c>RegionView.TextureFor</c> caches: nothing here changes texture per frame, only alpha.
    /// </summary>
    ITexture TextureFor(IRenderer renderer, string sprite)
    {
        if (!_textures.TryGetValue(sprite, out ITexture? texture))
        {
            texture = renderer.Textures.Load(sprite);
            _textures.Add(sprite, texture);
        }

        return texture;
    }
}
