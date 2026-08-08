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
/// <para>
/// One exception, and it is the reason the sentence above is worth stating rather than assuming:
/// bodies on the <c>"UI"</c> layer — today, the arrows that guide a new pilot out of the debris
/// field — are read and skipped here, because <see cref="HelpArrowView"/> draws them instead, and
/// it <em>does</em> know the ship exists, fading each one as the ship gets close. Drawing them
/// here too would put a second, un-fading copy underneath its own fade.
/// </para>
/// </remarks>
/// <param name="camera">The camera the world is drawn through — the same one the ship uses.</param>
public sealed class RegionView(ICamera camera) : IRenderable
{
    /// <summary>
    /// The order the layers this draws are drawn in, furthest first.
    /// </summary>
    /// <remarks>
    /// Named here rather than sorted alphabetically or taken in file order, because "which is
    /// behind which" is a decision rather than an accident of naming. A body in a layer this does
    /// not name is drawn last, so content that invents a layer appears rather than vanishing while
    /// nothing says why. <c>"UI"</c> is deliberately not among them — bodies on it are filtered out
    /// in <see cref="Scene"/> before sorting ever sees them, so it never has an order to hold here.
    /// </remarks>
    public static readonly string[] LayerOrder = ["Parallax", "Default", "Environment", "Characters"];

    readonly Dictionary<string, ITexture> _textures = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the colour the bodies painted on the sky are drawn through. Opaque white —
    /// what a region is drawn with until something says otherwise — draws them exactly as they
    /// were authored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reaches the backdrop and nothing else. What is painted on the sky is scenery the player
    /// looks past; what stands in the world is scenery the player flies into, and dimming that
    /// costs them the warning they get of what they are about to hit. So this darkens the picture
    /// behind the game without touching how legible the game is.
    /// </para>
    /// <para>
    /// A tint here rather than a second set of authored artwork, because how dark the sky should
    /// be is a decision about this screen and not about the region: a region flown through at
    /// another time of day is the same content lit differently.
    /// </para>
    /// </remarks>
    public Colour BackdropTint { get; set; } = Colour.White;

    /// <summary>
    /// The asset key the region's light sources are drawn with. An identifier, not text: it is
    /// never translated.
    /// </summary>
    /// <remarks>
    /// This is the one sprite a region authors that is a <em>light</em> rather than a thing, and
    /// the two below are what that costs it: it is placed by where the light is rather than by
    /// where the file's middle is, and it flickers. Named here rather than carried by the content,
    /// because whether a sprite is a light is a fact about the artwork — a body that had to declare
    /// it could be authored not to, and then a light in the field would quietly be a rock that
    /// glows.
    /// </remarks>
    public const string LightSprite = "glow";

    /// <summary>
    /// Where the light in <see cref="LightSprite"/> actually sits down its own file, as a fraction
    /// of the texture's height.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a half, because the art is not centred within its own bounds. <c>glow.png</c> is 241×183
    /// with a stretch of near-empty canvas above the light and almost none below it: measured from
    /// the shipped PNG, the lit part spans rows 52–171 and its alpha-weighted centre is 0.654 down
    /// the file, which every threshold from barely-visible to nearly-opaque agrees on to within a
    /// few hundredths. Across, it is centred to within half a percent and is left at a half.
    /// </para>
    /// <para>
    /// Drawn by the file's middle instead, a light lands roughly a third of a ship's length below
    /// the point it was placed at — which is what #188 reported as lights sitting on the debris
    /// rather than hanging in space among it. Corrected here rather than by redrawing the asset,
    /// for the reason <c>MenuScreen.TitleContentCentreFraction</c> is: this number describes the
    /// file, so a redrawn light restates it rather than silently moving every light in the region.
    /// </para>
    /// <para>
    /// It reaches the region's lights only. <c>ShipView</c> draws its engine glows from the same
    /// asset and is deliberately untouched — those offsets were placed against the origin they are
    /// drawn with, so correcting theirs would move flames that are currently where somebody put
    /// them.
    /// </para>
    /// </remarks>
    public const float LightArtworkCentreFraction = 0.65f;

    /// <summary>
    /// How dim a light gets at the bottom of its flicker, and how bright at the top, against the 1
    /// it was authored at.
    /// </summary>
    /// <remarks>
    /// The ceiling is the artwork itself: brighter than 1 is a light the author never drew, and the
    /// device would clamp it anyway, so the flicker only ever takes brightness away. The floor is
    /// the other half of the same thought — the lights are scattered through a debris field the
    /// player is navigating by, and one that dipped to nothing would blink out of the scene rather
    /// than flicker in it.
    ///
    /// Untuned, and said plainly: the depth and the rates below were chosen by reasoning about them
    /// rather than by looking at them, which is a judgement only a human at a screen can settle.
    /// </remarks>
    public const float DimmestLight = 0.6f;

    /// <inheritdoc cref="DimmestLight" />
    public const float BrightestLight = 1f;

    /// <summary>
    /// How fast a light breathes, in radians a second, and how much faster the tremor laid over it
    /// runs.
    /// </summary>
    /// <remarks>
    /// Two rates rather than one, at a ratio that is not a whole number, so the pair does not come
    /// back into step on any short cycle: a single sine is a pulse, and a pulse on a scattered set
    /// of lights reads as machinery rather than as light.
    /// </remarks>
    public const double FlickerRadiansPerSecond = 3.1;

    /// <inheritdoc cref="FlickerRadiansPerSecond" />
    public const double FlickerTremorRatio = 2.7;

    double _secondsElapsed;

    /// <summary>
    /// Gets or sets how long the region has been on screen, in seconds — the only thing the
    /// flicker is worked out from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view is told the time rather than keeping it, the same way <c>Orbs.PlaceAround</c> is
    /// handed how long has been flown: nothing here ticks and nothing accumulates, so a frame drawn
    /// twice draws the same picture both times and the flicker cannot drift with the frame rate.
    /// </para>
    /// <para>
    /// A value that is negative or not finite is refused where it is written, for the reason the
    /// camera refuses a target that is not finite. The sine of a number that is not one is not a
    /// number, so every light in the region would be drawn through a colour channel that is not a
    /// number — and <see cref="Colour"/> would raise it on some later frame, naming a channel
    /// rather than the clock that produced it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative, or is not a finite number.
    /// </exception>
    public double SecondsElapsed
    {
        get => _secondsElapsed;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The time a region has been on screen must be a finite number.");
            }

            _secondsElapsed = value;
        }
    }

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
                .Where(body => !string.Equals(body.Layer, HelpArrowView.Layer, StringComparison.Ordinal))
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

            // The sky takes the tint; everything standing in the world is drawn as authored.
            Colour colour = fixedToScreen ? BackdropTint : Colour.White;

            bool light = string.Equals(body.Sprite, LightSprite, StringComparison.Ordinal);

            renderer.Draw(new Sprite(
                Texture: texture,
                Position: screen,
                Rotation: rotation,
                Origin: OriginOf(texture, light),
                Scale: scale)
            {
                // A light dips and comes back; a rock is drawn at whatever the tint left it. The
                // flicker multiplies rather than replaces, so a light painted on a dimmed sky is
                // dimmed and flickering rather than one or the other.
                Colour = light ? Dimmed(colour, BrightnessAt(body.X, body.Y, _secondsElapsed)) : colour,
            });
        }
    }

    /// <summary>
    /// The point within a texture that a body is placed and turned about: the middle of the file
    /// for everything the region draws, and the middle of the <em>light</em> for a light.
    /// </summary>
    /// <remarks>
    /// Only the light has been measured, so only the light is corrected — see
    /// <see cref="LightArtworkCentreFraction"/>. That is deliberate rather than an omission: a
    /// solid body's collision rectangle is seeded from the body's own position, and moving where
    /// its sprite is drawn without moving what it collides at would put the two out of step.
    /// </remarks>
    static Vector2 OriginOf(ITexture texture, bool light) =>
        new(texture.Width / 2f, texture.Height * (light ? LightArtworkCentreFraction : 0.5f));

    /// <summary>
    /// The same colour with its three channels taken down by the amount given, leaving how much of
    /// it shows alone.
    /// </summary>
    static Colour Dimmed(Colour colour, float brightness) =>
        new(colour.Red * brightness, colour.Green * brightness, colour.Blue * brightness, colour.Alpha);

    /// <summary>
    /// How brightly a light standing at a given point is drawn at a given moment, against the 1 it
    /// was authored at.
    /// </summary>
    /// <param name="x">Where the light stands along the world's X axis.</param>
    /// <param name="y">Where the light stands along the world's Y axis.</param>
    /// <param name="seconds">How long the region has been on screen.</param>
    /// <returns>
    /// A brightness between <see cref="DimmestLight"/> and <see cref="BrightestLight"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A slow breath with a faster tremor laid over it, mixed so the two together can never leave
    /// the range the two constants name: the parts are weighted to sum to one, so the worst case is
    /// both at full stretch in the same direction, which is exactly the end of the range.
    /// </para>
    /// <para>
    /// Where a light stands is what sets its phase. A field of lights all dipping on the same beat
    /// reads as a power cut rather than as light, and taking the phase from the position means
    /// nothing has to be authored per light — and that a light keeps the phase it had when the
    /// region is loaded again, which a number handed out in draw order would not.
    /// </para>
    /// </remarks>
    public static float BrightnessAt(double x, double y, double seconds)
    {
        // Two coefficients rather than one, and unequal, so that lights sharing a row or a column
        // are still out of step with each other.
        double phase = (x * 0.7) + (y * 0.31);

        double breath = Math.Sin((seconds * FlickerRadiansPerSecond) + phase);
        double tremor = Math.Sin((seconds * FlickerRadiansPerSecond * FlickerTremorRatio) + (phase * 1.7));

        double midpoint = (BrightestLight + DimmestLight) / 2d;
        double depth = (BrightestLight - DimmestLight) / 2d;

        return (float)(midpoint + (depth * ((breath * 0.6) + (tremor * 0.4))));
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
