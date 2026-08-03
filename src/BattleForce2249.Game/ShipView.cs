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
    /// </remarks>
    public const float LengthInWorldUnits = 96f;

    string _assetKey = DefaultAssetKey;

    ITexture? _texture;

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
    public void Render(IRenderer renderer)
    {
        // Loaded on first draw rather than up front: the graphics device the texture belongs to
        // does not exist until the platform host has a window, which is after this is built.
        _texture ??= renderer.Textures.Load(_assetKey);

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
            Pose.Heading - camera.Orientation,
            origin,
            LengthInWorldUnits * camera.PixelsPerUnit / _texture.Height));
    }
}
