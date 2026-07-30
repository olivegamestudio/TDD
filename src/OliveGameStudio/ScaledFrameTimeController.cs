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
    /// stand still; a negative scale would rewind countdowns and animations.
    /// </exception>
    public double TimeScale
    {
        get => _timeScale;
        set => _timeScale = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Time scale cannot be negative.");
    }

    /// <summary>
    /// Applies the current time scale to a frame time.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>The scaled frame time that the game should advance by.</returns>
    public TimeSpan Filter(TimeSpan frameTime) => frameTime * _timeScale;
}
