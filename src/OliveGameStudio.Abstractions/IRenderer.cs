using System.Numerics;

namespace OliveGameStudio;

/// <summary>
/// The surface the game draws to for one frame. A platform host implements this over its own
/// graphics device; everything above it draws by describing sprites, so a test can substitute a
/// renderer that records them and assert what a screen drew without a window.
/// </summary>
public interface IRenderer
{
    /// <summary>
    /// The textures available to draw with.
    /// </summary>
    /// <remarks>
    /// Textures hang off the renderer rather than being injected because both belong to the
    /// same graphics device, and that device does not exist until the platform host has a
    /// window — later than the container is built. Reaching them through the renderer means a
    /// drawable can load on first draw, by which time the device is certainly there.
    /// </remarks>
    ITextureLoader Textures { get; }

    /// <summary>
    /// The size of the drawable area, in pixels. Read per frame rather than cached, because a
    /// desktop window can be resized between one frame and the next.
    /// </summary>
    Vector2 ViewportSize { get; }

    /// <summary>
    /// Draws a sprite. Sprites drawn later in a frame appear over sprites drawn earlier, so the
    /// order a screen issues them in is the order they stack.
    /// </summary>
    /// <param name="sprite">What to draw, already in screen space.</param>
    void Draw(Sprite sprite);
}
