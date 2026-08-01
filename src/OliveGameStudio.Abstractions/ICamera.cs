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
    /// The world position held at the centre of the viewport. Finite on both axes.
    /// </summary>
    /// <remarks>
    /// An implementation is expected to refuse a non-finite position where it is assigned rather
    /// than carry it into <see cref="WorldToScreen"/>. Everything in the world is drawn through
    /// one camera, so a <c>NaN</c> here is not one sprite out of place — it is every sprite in
    /// the game drawn nowhere, which reads as a blank screen and not as a fault. Refusing it
    /// where it is written is what makes the failure name the frame that produced the number.
    /// </remarks>
    Vector2 Target { get; set; }

    /// <summary>
    /// How many screen pixels one world unit occupies. This is the zoom: raising it makes the
    /// world bigger on screen without changing a single world coordinate. Finite, and above zero.
    /// </summary>
    /// <remarks>
    /// Held to the same rule as <see cref="Target"/>, and for the same reason. Zero is excluded
    /// as well as the non-finite values: it scales the world onto a single point, and a caller
    /// working out how much world a viewport covers divides by it.
    /// </remarks>
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
