namespace OliveGameStudio;

/// <summary>
/// An <see cref="IFrameTimeController"/> that multiplies each frame time by a scale factor,
/// so the game can run slower or faster than real time. A scale of 1 runs at normal speed and
/// a fractional scale runs in slow motion.
/// A scale of 0 does freeze the game, but prefer <see cref="PausableFrameTimeController"/>
/// when a plain pause is all that is wanted.
/// The scale is runtime state driven by the game, not a startup setting, so it always begins
/// at real time and is changed by whatever gameplay calls for it.
/// </summary>
public sealed class ScaledFrameTimeController : IFrameTimeController
{
    double _timeScale = 1;

    /// <summary>
    /// Gets or sets the factor applied to each frame time. Defaults to 1, which leaves the
    /// frame time unchanged.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when set to a negative value. Time is only ever allowed to move forwards or
    /// stand still; a negative scale would rewind countdowns and animations. Also thrown when
    /// set to a value that is not a finite number, because no such scale names a frame time the
    /// game could advance by.
    /// </exception>
    public double TimeScale
    {
        get => _timeScale;

        // Finiteness is asked ahead of the sign, because an ordered comparison does not answer
        // the question this guard is for. `value >= 0` refuses NaN and negative infinity by
        // accident — both are false against zero — and lets positive infinity through, which is
        // the one of the three that then takes the game down: `frameTime * infinity` is more ticks
        // than a TimeSpan holds, so Filter throws OverflowException on every frame from the frame
        // loop, naming TimeSpan arithmetic rather than the scale that caused it. Stating it as
        // "finite, and not negative" refuses all three where the value is written, which is the
        // same answer Camera2D gives for the same reason: a value that cannot be used is refused
        // at the setter, not diagnosed from the frame it eventually breaks.
        set => _timeScale = double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value), value, "Time scale must be a finite number and cannot be negative.");
    }

    /// <summary>
    /// Applies the current time scale to a frame time.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>The scaled frame time that the game should advance by.</returns>
    public TimeSpan Filter(TimeSpan frameTime) => frameTime * _timeScale;
}
