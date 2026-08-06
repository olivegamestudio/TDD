using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Draws a region's scenery: everything authored into the world that is looked at rather than
/// reacted to.
/// </summary>
/// <remarks>
/// <para>
/// It draws <see cref="SceneDefinition.Bodies"/> and nothing else. Markers are places the game
/// reacts to and have no appearance of their own — a save zone is not a thing you can see, and
/// drawing one would put the editor's gizmos in front of the player.
/// </para>
/// <para>
/// <b>It follows the camera, not the ship</b>, exactly as the star field does. Nothing here knows
/// a ship exists; it draws what is in the world at wherever the camera is pointed, so it stays
/// correct the day something else is being followed.
/// </para>
/// </remarks>
/// <param name="camera">The camera the world is drawn through — the same one the ship uses.</param>
public sealed class RegionView(ICamera camera) : IRenderable
{
    /// <summary>
    /// The order the layers are drawn in, furthest first.
    /// </summary>
    /// <remarks>
    /// Named here rather than sorted alphabetically or taken in file order, because "which is
    /// behind which" is a decision rather than an accident of naming. A body in a layer this does
    /// not name is drawn last, so content that invents a layer appears rather than vanishing while
    /// nothing says why.
    /// </remarks>
    public static readonly string[] LayerOrder = ["Parallax", "Default", "Environment", "Characters", "UI"];

    readonly Dictionary<string, ITexture> _textures = new(StringComparer.Ordinal);

    SceneDefinition _scene = SceneDefinition.Empty;
    IReadOnlyList<SceneBody> _ordered = [];

    /// <summary>
    /// Gets or sets the region being drawn. Setting it re-sorts the scenery into drawing order,
    /// which is done once here rather than every frame: a region does not change while it is being
    /// flown through, and sorting hundreds of bodies per frame to reach the same answer is a cost
    /// paid sixty times a second for nothing.
    /// </summary>
    public SceneDefinition Scene
    {
        get => _scene;
        set
        {
            _scene = value;
            _ordered = [.. value.Bodies
                .OrderBy(body => LayerIndex(body.Layer))
                .ThenBy(body => body.Order)];
        }
    }

    /// <summary>
    /// How far back a layer sits. An unknown layer sorts last — see <see cref="LayerOrder"/>.
    /// </summary>
    static int LayerIndex(string layer)
    {
        int index = Array.IndexOf(LayerOrder, layer);
        return index < 0 ? LayerOrder.Length : index;
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        foreach (SceneBody body in _ordered)
        {
            ITexture texture = TextureFor(renderer, body.Sprite);

            bool fixedToScreen = body.Parallax <= 0;

            Vector2 world = new((float)body.X, (float)body.Y);

            // One scale, from the average of the two the content carries. Sprite draws with a
            // single factor, so a body squashed on one axis cannot be drawn as authored — this
            // keeps its area right rather than its shape, and is the thing to revisit when a
            // sprite can be drawn with two.
            float authored = (float)((body.ScaleX + body.ScaleY) / 2);

            Vector2 screen;
            // Negated, because the two conventions turn opposite ways. The content was authored
            // where a positive angle turns anticlockwise and Y points up; a Sprite's rotation is
            // clockwise, because the screen's Y axis points down. Taken across unchanged, every
            // rock authored at an angle lands mirrored — which reads as scenery that is subtly
            // wrong everywhere rather than as anything obviously broken.
            float rotation = (float)(-body.RotationDegrees * Math.PI / 180);
            float scale;

            if (fixedToScreen)
            {
                // Painted on the sky. It takes none of the camera's movement and none of its turn,
                // so flying past it — or turning under it — leaves it exactly where it was.
                //
                // It is also measured against the *view* rather than the world: what matters about
                // a backdrop is how much of the screen it fills, so it is laid out as though this
                // window were the one it was authored against. That is what lets the same star
                // field cover a small window and a large one.
                float ofView = renderer.ViewportSize.Y / AuthoredViewHeightInPixels;

                screen = (renderer.ViewportSize / 2f)
                    + (world * AuthoredPixelsPerUnit * ofView);
                scale = authored * ofView;
            }
            else
            {
                screen = camera.WorldToScreen(world, renderer.ViewportSize);

                // Put through the camera rather than adjusted by hand. The body's own angle is
                // its own; what the camera decides is how that angle looks from where it stands.
                rotation = camera.WorldToScreenRotation(rotation);
                scale = authored * camera.PixelsPerUnit / AuthoredPixelsPerUnit;
            }

            renderer.Draw(new Sprite(
                Texture: texture,
                Position: screen,
                Rotation: rotation,
                Origin: new Vector2(texture.Width, texture.Height) / 2f,
                Scale: scale));
        }
    }

    /// <summary>
    /// How many pixels of a body's texture the author took to be one unit.
    /// </summary>
    /// <remarks>
    /// The scenery was authored in a tool where a sprite at scale 1 covered this many pixels per
    /// unit. Without it, a rock authored at scale 1 would be drawn at its full texture size
    /// whatever the zoom, and a debris field would be a wall.
    /// </remarks>
    public const float AuthoredPixelsPerUnit = 100f;

    /// <summary>
    /// How tall the view a backdrop was composed against was, in those same pixels.
    /// </summary>
    /// <remarks>
    /// Backdrops are laid out against the window rather than the world, so this is what "full
    /// screen" meant to whoever placed them. A window taller than this shows more of the sky and a
    /// shorter one less, which is what keeps a backdrop covering the screen at any size instead of
    /// leaving a band of black at one edge.
    /// </remarks>
    public const float AuthoredViewHeightInPixels = 1080f;

    /// <summary>
    /// The texture for an asset key, loaded once and kept. A region draws the same handful of
    /// sprites hundreds of times, so this is the difference between a few loads and a few hundred
    /// dictionary lookups inside the loader every frame.
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
