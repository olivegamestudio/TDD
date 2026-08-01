using System.Numerics;

namespace OliveGameStudio;

/// <summary>
/// The view onto the world: which point the viewport is centred on, and how many pixels a world
/// unit is worth. Everything drawn in the world goes through one camera, so the ship, the
/// background and anything added later agree about where a world position lands on screen.
/// </summary>
/// <remarks>
/// The camera holds no opinion about what it follows. Whatever owns the frame decides that —
/// the game screen points it at the ship — which keeps a chase camera and a fixed one the same
/// object with a different <see cref="Target"/>.
/// </remarks>
public interface ICamera
{
    /// <summary>
    /// The world position held at the centre of the viewport.
    /// </summary>
    Vector2 Target { get; set; }

    /// <summary>
    /// How many screen pixels one world unit occupies. This is the zoom: raising it makes the
    /// world bigger on screen without changing a single world coordinate.
    /// </summary>
    float PixelsPerUnit { get; set; }

    /// <summary>
    /// Converts a world position into the pixel it occupies on screen.
    /// </summary>
    /// <param name="world">The position in world units.</param>
    /// <param name="viewportSize">
    /// The size of the drawable area in pixels, as reported by the frame's
    /// <see cref="IRenderer.ViewportSize"/>. Passed per call rather than held, so a resized
    /// window needs no notification to stay correct.
    /// </param>
    /// <returns>The position in pixels from the top left of the viewport.</returns>
    Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize);
}
