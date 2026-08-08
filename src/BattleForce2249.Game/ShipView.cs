using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Draws the player's ship at its current pose, through the game's camera.
/// </summary>
/// <param name="camera">The camera the world is drawn through.</param>
public sealed class ShipView(ICamera camera) : IShipView
{
    /// <summary>
    /// The asset key drawn until something says otherwise — the ship the player starts with.
    /// An identifier, not text: it is never translated.
    /// </summary>
    public const string DefaultAssetKey = "ship1";

    /// <summary>
    /// How long the ship is, nose to engines, in world units.
    /// </summary>
    /// <remarks>
    /// The ship is sized in world units rather than pixels so it stays the same size relative to
    /// the world it flies through — a zoom changes how much of the world is on screen, not how
    /// big the ship is in it. The artwork is far larger than this; the scale is derived from the
    /// texture below, so replacing the sprite with one of another size changes nothing here.
    ///
    /// It shipped at 96, which was chosen against an empty starfield where there was nothing to
    /// judge it against. Beside the debris the ship is meant to be flown through it read as far
    /// too large — the scenery is the thing that gives the ship a size, so the number moved once
    /// there was scenery.
    /// </remarks>
    public const float LengthInWorldUnits = 30f;

    /// <summary>
    /// The asset key the engine glow is drawn with. An identifier, not text: it is never
    /// translated.
    /// </summary>
    public const string EngineGlowAssetKey = "glow";

    /// <summary>
    /// Where the ship's engines glow, in the ship's own frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six of them, front and rear, taken from the bodies the opening region was authored with —
    /// see <see cref="ShipEngineGlow"/> for why they are no longer in the region. The values are
    /// the authored ones, unchanged: this makes the glow follow the ship, and leaves how the glow
    /// looks exactly as the author left it.
    /// </para>
    /// <para>
    /// Held here beside <see cref="DefaultAssetKey"/> rather than in the world, because a glow
    /// cast by the ship's engines is part of the picture of the ship in the same way its hull
    /// sprite is. It is shared by every ship drawn through this view, which is right while there
    /// is one ship to draw and is the thing to revisit the day a second ship has engines
    /// somewhere else.
    /// </para>
    /// <para>
    /// Three of each: the bow thrusters (<see cref="EngineGroup.Fore"/>) that fire astern, and the
    /// mains (<see cref="EngineGroup.Aft"/>) that fire ahead — see <see cref="Thrust"/> for what
    /// decides which set is actually drawn on a given frame.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<ShipEngineGlow> EngineGlows =
    [
        new(new Vector2(0f, 0f), 0f, 4.5f, 18f, EngineGroup.Fore),
        new(new Vector2(-5.625f, 0f), 20f, 2.25f, 9f, EngineGroup.Fore),
        new(new Vector2(5.625f, 0f), 340f, 2.25f, 9f, EngineGroup.Fore),
        new(new Vector2(0f, 0f), 180f, 4.5f, 18f, EngineGroup.Aft),
        new(new Vector2(-6.75f, 4.5f), 190f, 2.25f, 9f, EngineGroup.Aft),
        new(new Vector2(6.75f, 4.5f), 170f, 2.25f, 9f, EngineGroup.Aft),
    ];

    string _assetKey = DefaultAssetKey;

    ITexture? _texture;

    ITexture? _glowTexture;

    /// <inheritdoc />
    public string AssetKey
    {
        get => _assetKey;

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            if (string.Equals(_assetKey, value, StringComparison.Ordinal))
            {
                return;
            }

            _assetKey = value;

            // Dropped rather than reloaded here: loading belongs to the draw, which is the only
            // place a graphics device is known to exist.
            _texture = null;
        }
    }

    /// <inheritdoc />
    public ShipPose Pose { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// Defaults to <c>0</c> — coasting — so a ship nobody has flown yet, or one drawn between the
    /// screen loading and the first frame reaching it, burns nothing.
    /// </remarks>
    public float Thrust { get; set; }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        // Loaded on first draw rather than up front: the graphics device the texture belongs to
        // does not exist until the platform host has a window, which is after this is built.
        _texture ??= renderer.Textures.Load(_assetKey);

        // Loaded whether or not any engine is currently lit, so the first frame a key is pressed
        // is not the one that pays for a texture load — a glow that stutters in on its first use
        // reads as a rendering bug rather than as the engine catching.
        _glowTexture ??= renderer.Textures.Load(EngineGlowAssetKey);

        // The engines first, so the hull is drawn over its own glow rather than under it. Only the
        // group the current thrust actually fires is drawn — a bow thruster glowing while the ship
        // is under main power, or every engine burning at a dead stop, is exactly the "floating and
        // do not follow the ship" report this glow was already rebuilt once for, one layer up: it
        // has to answer to the keypress that is supposedly driving it, not just to the ship's pose.
        foreach (ShipEngineGlow glow in EngineGlows)
        {
            if (!Fires(glow.Group, Thrust))
            {
                continue;
            }

            RenderGlow(renderer, _glowTexture, glow);
        }

        // The origin is the middle of the sprite, so the ship turns about itself rather than
        // swinging around its top left corner.
        Vector2 origin = new(_texture.Width / 2f, _texture.Height / 2f);

        // The artwork faces up the screen, which is world forward, so a heading of zero on an
        // upright camera needs no rotation. What is drawn is the part of the heading the camera
        // is not already holding: point the camera at the ship's own heading — which is what the
        // game screen does — and that difference is zero, so the ship keeps its nose at the top
        // of the window while the world turns around it. Subtracting rather than passing zero is
        // what keeps this right for a camera that is *not* following the ship's heading, which is
        // every other camera a scene might be drawn through.
        renderer.Draw(new Sprite(
            _texture,
            camera.WorldToScreen(Pose.Position, renderer.ViewportSize),
            camera.WorldToScreenRotation(Pose.Heading),
            origin,
            LengthInWorldUnits * camera.PixelsPerUnit / _texture.Height));
    }

    /// <summary>
    /// Whether an engine group is burning at the given thrust.
    /// </summary>
    /// <remarks>
    /// Exactly the sign of <see cref="Thrust"/> and nothing softer: the glow is a readout of the
    /// keypress, not a physics quantity of its own, so a nudge off dead centre lights the engine
    /// the same as a full press does. Coasting fires neither group.
    /// </remarks>
    static bool Fires(EngineGroup group, float thrust) => group switch
    {
        EngineGroup.Aft => thrust > 0f,
        EngineGroup.Fore => thrust < 0f,
        _ => false,
    };

    /// <summary>
    /// Draws one engine glow, in the ship's frame rather than the world's.
    /// </summary>
    /// <remarks>
    /// Every number the glow carries is read against the ship: its offset is turned by the
    /// heading before it is added to the ship's position, and its own angle is added to the
    /// heading before the camera is asked what that looks like. That pair is the whole of being
    /// parented — a glow whose offset was added in world axes would swing round to the wrong side
    /// of the hull as the ship came about, and one drawn at its own angle alone would go on
    /// pointing the way it was authored while the ship turned underneath it.
    /// </remarks>
    void RenderGlow(IRenderer renderer, ITexture texture, ShipEngineGlow glow)
    {
        // Negated for the reason the scenery's rotation is: the content was authored where a
        // positive angle turns anticlockwise, and a sprite's turns clockwise.
        float own = -glow.RotationDegrees * MathF.PI / 180f;

        renderer.Draw(new Sprite(
            texture,
            camera.WorldToScreen(Pose.Position + Turn(glow.Offset, Pose.Heading), renderer.ViewportSize),
            camera.WorldToScreenRotation(Pose.Heading + own),
            new Vector2(texture.Width, texture.Height) / 2f,

            // One texture pixel to one world unit at the scale the content was authored against,
            // with the authored stretch on top. Sized this way rather than from a length in world
            // units, so replacing the artwork with a sprite of another size changes nothing here —
            // the same reason the hull's scale is derived from its texture.
            camera.PixelsPerUnit / RegionView.AuthoredPixelsPerUnit)
        {
            Stretch = new Vector2(glow.ScaleAcross, glow.ScaleAlong),
        });
    }

    /// <summary>
    /// Turns an offset in the ship's frame — X to starboard, Y forward — into the world's.
    /// </summary>
    /// <remarks>
    /// A heading of zero is straight up the world's positive Y axis and the angle increases to
    /// starboard, which is the same agreement the pose itself is written under. So forward is
    /// (sin, cos) and starboard is (cos, −sin), and at a heading of zero this hands back exactly
    /// what it was given.
    /// </remarks>
    static Vector2 Turn(Vector2 shipFrame, float heading)
    {
        float sin = MathF.Sin(heading);
        float cos = MathF.Cos(heading);

        return new Vector2(
            (shipFrame.X * cos) + (shipFrame.Y * sin),
            (shipFrame.Y * cos) - (shipFrame.X * sin));
    }
}
