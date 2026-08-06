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
    /// The world position held at the centre of the viewport. Both axes must be finite;
    /// how far out the position is, is not bounded.
    /// </summary>
    /// <remarks>
    /// The finiteness is part of the contract rather than one implementation's caution, because
    /// the target is subtracted from every world position that is drawn. A value that is not a
    /// number therefore does not degrade the picture — it puts everything drawn through the camera
    /// at a position that is nowhere, in full and without an error, and what the player sees is a
    /// blank window. An implementation is expected to refuse it where it is assigned, so that the
    /// failure names the frame that produced the number; a renderable cannot usefully check it,
    /// because then every renderable ever written would have to, each in its own way.
    /// </remarks>
    Vector2 Target { get; set; }

    /// <summary>
    /// How many screen pixels one world unit occupies. This is the zoom: raising it makes the
    /// world bigger on screen without changing a single world coordinate. It must be finite and
    /// above zero.
    /// </summary>
    /// <remarks>
    /// Multiplied into every world offset, so a value that is not a number scatters the world in
    /// exactly the way <see cref="Target"/> describes, and a zoom of zero collapses it to a point.
    /// Refused where it is assigned for the same reason.
    /// </remarks>
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
    /// It turns the world and nothing else. Anything drawn outside the camera — every menu and
    /// every future HUD — is untouched by it. A sprite that is meant to stay aligned with the
    /// world puts its own angle through <see cref="WorldToScreenRotation"/> rather than doing the
    /// arithmetic itself.
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

    /// <summary>
    /// Converts an angle in the world into the angle it is drawn at on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of <see cref="WorldToScreen"/>. Placing a body takes both — where it is and
    /// which way it faces — and the two have to agree about which way the camera turned or the
    /// body lands in the right place pointing the wrong way. That reads as the world spinning
    /// rather than as anything to do with rotation, so it is worth the two being one decision made
    /// in one place.
    /// </para>
    /// <para>
    /// <b>A body's own angle is its own.</b> This does not change what a rock is turned to in the
    /// world; it answers what that turn looks like from where the camera is standing. Nothing
    /// calling it should be folding the camera's orientation in by hand — that was duplicated
    /// across every view that drew anything, and each copy was a chance to get the sign wrong
    /// independently.
    /// </para>
    /// </remarks>
    /// <param name="worldRotation">
    /// The angle in the world, in radians, measured the way the world measures angles.
    /// </param>
    /// <returns>The clockwise angle to draw at, in radians, as <see cref="Sprite.Rotation"/> wants.</returns>
    float WorldToScreenRotation(float worldRotation);
}
