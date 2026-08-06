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

    Vector2 _target;

    float _pixelsPerUnit = 1f;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either axis is not finite. Refused where it is written for the same reason
    /// <see cref="Orientation"/> is: the target is subtracted from every world position drawn, so
    /// a value that is not a number puts the <em>whole world</em> — the stars, the ship, anything
    /// added later — at a position that is nowhere. It is drawn in full, without an error, and the
    /// symptom is a blank window rather than anything naming the frame that produced it.
    /// </exception>
    /// <remarks>
    /// A refused assignment leaves the camera on its last good target, so it stays usable. There
    /// is deliberately no bound on how far out the target is: the world is unbounded and the
    /// player flies forward indefinitely, so distance is not what is being refused here — only a
    /// number that is not one.
    /// </remarks>
    public Vector2 Target
    {
        get => _target;

        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A camera's target must be a finite world position on both axes.");
            }

            _target = value;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not finite, or is not above zero. The zoom multiplies every world offset, so a
    /// value that is not a number scatters the world exactly as a non-finite <see cref="Target"/>
    /// does; a zoom of nothing collapses it to a point and a negative one turns it inside out.
    /// This used to be left to whichever caller remembered to check, which was one caller, testing
    /// it with an ordered comparison — and every ordered comparison against
    /// <see cref="float.NaN"/> is false, so the one value the check existed to stop was the one
    /// value that walked past it.
    /// </exception>
    /// <remarks>
    /// One pixel per unit by default, so a world distance reads as a pixel distance until
    /// somebody deliberately decides otherwise. A very small zoom is deliberately still allowed:
    /// the star field's clipping when it is wound far down is a stated limit of the star field,
    /// not a mistake for the camera to veto. Anything animating this must not pass through zero on
    /// the way.
    /// </remarks>
    public float PixelsPerUnit
    {
        get => _pixelsPerUnit;

        set
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A camera's zoom must be a finite number of pixels per world unit, above zero.");
            }

            _pixelsPerUnit = value;
        }
    }

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

    /// <inheritdoc />
    public float WorldToScreenRotation(float worldRotation) => worldRotation - _orientation;
}
