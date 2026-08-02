using System.Numerics;

namespace OliveGameStudio;

/// <summary>
/// A camera over a top-down world, centring the viewport on <see cref="Target"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two conventions are pinned here, and everything drawn in the world inherits them.
/// </para>
/// <para>
/// <b>The world's Y axis points forward; the screen's points down.</b> The world is described the
/// way the fiction describes it — forward is the positive Y axis — while a screen counts pixels
/// downward from its top left. This class is the one place that reconciles them, so nothing else
/// has to remember which way up it is: fly forward and the world moves down the screen.
/// </para>
/// <para>
/// <b>Distances are world units until the last moment.</b> Positions, speeds and quest markers
/// are all in world units; <see cref="PixelsPerUnit"/> converts once, here. That is what keeps a
/// zoom, or a different window size, from being a change to the physics.
/// </para>
/// </remarks>
public sealed class Camera2D : ICamera
{
    /// <inheritdoc />
    public Vector2 Target { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// One pixel per unit by default, so a world distance reads as a pixel distance until
    /// somebody deliberately decides otherwise.
    /// </remarks>
    public float PixelsPerUnit { get; set; } = 1f;

    /// <inheritdoc />
    public Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize)
    {
        Vector2 fromTarget = (world - Target) * PixelsPerUnit;

        // X carries straight across; Y is negated because world forward is screen up.
        return new Vector2(
            viewportSize.X / 2f + fromTarget.X,
            viewportSize.Y / 2f - fromTarget.Y);
    }
}
