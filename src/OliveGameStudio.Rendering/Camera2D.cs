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
/// <b>Both inputs are refused where they are written, not where they are read.</b> Everything
/// drawn in the world goes through <see cref="WorldToScreen"/>, so a <c>NaN</c> in either member
/// is not one sprite in the wrong place — it is every sprite in the game drawn at a position that
/// is nowhere, which looks like a blank screen rather than like an error. Guarding at each caller
/// would mean every renderable added afterwards had to remember to, so the check is here, once,
/// where the number arrives: the stack trace then names the frame that produced it.
/// </para>
/// </remarks>
public sealed class Camera2D : ICamera
{
    Vector2 _target;

    float _pixelsPerUnit = 1f;

    /// <inheritdoc />
    /// <remarks>
    /// Refused unless both axes are finite. There is no bound on how far out it may be — the
    /// world is unbounded and the player flies on — so finiteness is the whole rule.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Either axis of <paramref name="value"/> is not a finite number.
    /// </exception>
    public Vector2 Target
    {
        get => _target;

        set
        {
            // Named per axis, because "Target must be finite, but was <0, NaN>" says which rule
            // was broken and not which of two numbers to go and look at.
            ThrowIfNotFinite(value.X, $"{nameof(Target)}.X");
            ThrowIfNotFinite(value.Y, $"{nameof(Target)}.Y");

            _target = value;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// One pixel per unit by default, so a world distance reads as a pixel distance until
    /// somebody deliberately decides otherwise.
    /// </para>
    /// <para>
    /// Refused unless it is finite and above zero. Finiteness is asked separately rather than
    /// left to the comparison: <c>NaN &lt;= 0f</c> is false, so a guard written as a range check
    /// alone passes the one value that makes every result of the transform meaningless. Zero
    /// scales the world onto a single point, and anything dividing a viewport by the zoom to work
    /// out what it covers divides by nothing; a negative zoom mirrors the world without ever
    /// saying so.
    /// </para>
    /// <para>
    /// A very small zoom is <em>not</em> refused. A background that clips what it draws at a low
    /// enough zoom is a limit it states for itself, not a mistake in the camera, so the setter
    /// does not decide it is one.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is not a finite number, or is not above zero.
    /// </exception>
    public float PixelsPerUnit
    {
        get => _pixelsPerUnit;

        set
        {
            // First, and on its own: the comparison below is an ordered one, which NaN passes
            // whichever way round it is written.
            ThrowIfNotFinite(value, nameof(PixelsPerUnit));

            if (value <= 0f)
            {
                throw new ArgumentException(
                    $"A camera's {nameof(PixelsPerUnit)} must be above zero, but was {value}. A "
                    + "zoom of nothing scales the world onto a single point, and a negative one "
                    + "mirrors it.",
                    nameof(value));
            }

            _pixelsPerUnit = value;
        }
    }

    /// <inheritdoc />
    public Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize)
    {
        Vector2 fromTarget = (world - Target) * PixelsPerUnit;

        // X carries straight across; Y is negated because world forward is screen up.
        return new Vector2(
            viewportSize.X / 2f + fromTarget.X,
            viewportSize.Y / 2f - fromTarget.Y);
    }

    /// <summary>
    /// Refuses a number the camera cannot transform through, naming the member it was assigned to.
    /// </summary>
    /// <param name="value">The number being assigned.</param>
    /// <param name="member">The member — and axis, where there is one — it was assigned to.</param>
    static void ThrowIfNotFinite(float value, string member)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentException(
                $"A camera's {member} must be a finite number, but was {value}. Every world "
                + "position in the game is transformed through the camera, so this does not move "
                + "one sprite — it draws all of them at a position that is nowhere, which shows "
                + "as a blank screen rather than as an error.",
                "value"); // the setter's implicit parameter, which nameof cannot reach here
        }
    }
}
