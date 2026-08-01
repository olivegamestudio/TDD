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
    /// The asset key of the ship's sprite, as the content build names it. An identifier, not
    /// text: it is never translated.
    /// </summary>
    public const string TextureKey = "ship1";

    /// <summary>
    /// How long the ship is, nose to engines, in world units.
    /// </summary>
    /// <remarks>
    /// The ship is sized in world units rather than pixels so it stays the same size relative to
    /// the world it flies through — a zoom changes how much of the world is on screen, not how
    /// big the ship is in it. The artwork is far larger than this; the scale is derived from the
    /// texture below, so replacing the sprite with one of another size changes nothing here.
    /// </remarks>
    public const float LengthInWorldUnits = 96f;

    ITexture? _texture;

    /// <inheritdoc />
    public ShipPose Pose { get; set; }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        // Loaded on first draw rather than up front: the graphics device the texture belongs to
        // does not exist until the platform host has a window, which is after this is built.
        _texture ??= renderer.Textures.Load(TextureKey);

        // The origin is the middle of the sprite, so the ship turns about itself rather than
        // swinging around its top left corner.
        Vector2 origin = new(_texture.Width / 2f, _texture.Height / 2f);

        // The artwork faces up the screen, which is world forward, so a heading of zero needs no
        // rotation and the heading can be passed straight through.
        renderer.Draw(new Sprite(
            _texture,
            camera.WorldToScreen(Pose.Position, renderer.ViewportSize),
            Pose.Heading,
            origin,
            LengthInWorldUnits * camera.PixelsPerUnit / _texture.Height));
    }
}
