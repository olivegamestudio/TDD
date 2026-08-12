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
        //
        // Finiteness is the whole of what it refuses, though. A finite scale large enough to
        // overflow a particular frame time is not refused here, because whether it overflows is
        // not a fact about the scale — see Filter, which is where both numbers are known.
        set => _timeScale = double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value), value, "Time scale must be a finite number and cannot be negative.");
    }

    /// <summary>
    /// Applies the current time scale to a frame time.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>
    /// The scaled frame time that the game should advance by, saturated at
    /// <see cref="TimeSpan.MaxValue"/> — or <see cref="TimeSpan.MinValue"/> — when the scale asks
    /// for more time than a <see cref="TimeSpan"/> can hold.
    /// </returns>
    /// <remarks>
    /// The saturation lives here rather than in the setter because the setter cannot answer the
    /// question. Whether a scale overflows depends on the frame time it is multiplied by, and the
    /// setter has never seen one: a scale of a trillion is perfectly representable against a 16ms
    /// frame and is not against a frame of a day. So the setter refuses what it can decide from
    /// the value alone — a sign and a finiteness — and the frame time is where the rest is
    /// decided, on the only line that knows both numbers.
    /// </remarks>
    public TimeSpan Filter(TimeSpan frameTime)
    {
        // TimeSpan's own operator * does this multiplication and then throws OverflowException
        // past its range, which is the frame loop going down over a scale the game itself set.
        // Rounding matches what that operator does, so the scales that always worked are
        // unchanged; only the ones that used to throw come out anywhere different.
        double ticks = Math.Round(frameTime.Ticks * _timeScale);

        if (ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        if (ticks <= TimeSpan.MinValue.Ticks)
        {
            return TimeSpan.MinValue;
        }

        return new TimeSpan((long)ticks);
    }
}
