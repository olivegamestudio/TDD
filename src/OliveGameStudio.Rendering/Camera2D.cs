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
/// <para>
/// <b>Which way is up is the camera's to decide.</b> <see cref="Orientation"/> turns the world
/// about <see cref="Target"/>, so a ship can be held pointing up the screen without anything
/// that draws it knowing that is what is happening.
/// </para>
/// </remarks>
public sealed class Camera2D : ICamera
{
    float _orientation;

    /// <inheritdoc />
    public Vector2 Target { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// One pixel per unit by default, so a world distance reads as a pixel distance until
    /// somebody deliberately decides otherwise.
    /// </remarks>
    public float PixelsPerUnit { get; set; } = 1f;

    /// <inheritdoc />
    /// <remarks>
    /// Upright by default, so a camera nobody turns draws exactly what it drew before there was
    /// anything to turn — zero is passed through the transform below untouched rather than
    /// approximately.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not finite. This is refused where it is written rather than where it is read
    /// because of how it fails otherwise: the sine and cosine of a non-finite angle are
    /// <see cref="float.NaN"/>, so <em>every</em> sprite in the world is drawn — in full, with no
    /// error — at a position that is nowhere, and the symptom is a blank window rather than
    /// anything naming the frame that produced it.
    /// </exception>
    public float Orientation
    {
        get => _orientation;

        set
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A camera's orientation must be a finite angle in radians.");
            }

            _orientation = value;
        }
    }

    /// <inheritdoc />
    public Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize)
    {
        Vector2 fromTarget = (world - Target) * PixelsPerUnit;

        // Turned about the target, so that whatever lies along the orientation ends up dead
        // ahead. It reads as a positive rotation by the orientation rather than the negative one
        // an upright maths convention would ask for, because the world's angle increases to
        // starboard while its axes are the other handedness — the same reconciliation this class
        // performs for the Y axis, one line further down.
        float sin = MathF.Sin(_orientation);
        float cos = MathF.Cos(_orientation);

        Vector2 turned = new(
            (fromTarget.X * cos) - (fromTarget.Y * sin),
            (fromTarget.X * sin) + (fromTarget.Y * cos));

        // X carries straight across; Y is negated because world forward is screen up.
        return new Vector2(
            viewportSize.X / 2f + turned.X,
            viewportSize.Y / 2f - turned.Y);
    }
}
