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
    /// Which world heading is held pointing up the screen, in radians. Zero leaves the world
    /// upright, where world forward is screen up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured exactly the way a ship's heading is — zero along the positive world Y axis, the
    /// angle increasing to starboard — so whatever is being followed can hand its own heading
    /// straight over. A conversion here would turn the world the wrong way, which is the same
    /// agreement <c>ShipPose.Heading</c> already asks of both sides of the ship.
    /// </para>
    /// <para>
    /// The world turns about <see cref="Target"/> rather than about the world origin, so the
    /// point being followed keeps the middle of the viewport however far the camera turns. That
    /// is the whole of what makes the ship appear to point forward: the ship is drawn where it
    /// is and the world rotates around it, rather than the ship being spun to face the top of
    /// the window.
    /// </para>
    /// <para>
    /// It turns the world and nothing else. A sprite that is meant to stay aligned with the
    /// world has to take this off its own rotation — see <c>ShipView</c> — and anything drawn
    /// outside the camera, which is every menu and every future HUD, is untouched by it.
    /// </para>
    /// </remarks>
    float Orientation { get; set; }

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
