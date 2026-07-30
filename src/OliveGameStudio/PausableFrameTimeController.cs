namespace OliveGameStudio;

/// <summary>
/// An <see cref="IFrameTimeController"/> that gates the frame time on an explicit paused flag,
/// reporting zero elapsed time while paused and the real frame time otherwise.
/// Unlike <see cref="ScaledFrameTimeController"/> this is a binary stop with no notion of rate,
/// so resuming always returns to real time rather than to a previously set scale.
/// </summary>
public sealed class PausableFrameTimeController : IFrameTimeController
{
    /// <summary>
    /// Gets a value indicating whether the game is currently paused.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Pauses the game. Frames continue to arrive but none of them advance it.
    /// </summary>
    public void Pause() => IsPaused = true;

    /// <summary>
    /// Resumes the game at real time.
    /// </summary>
    public void Resume() => IsPaused = false;

    /// <summary>
    /// Flips between paused and running, for wiring straight to a single input.
    /// </summary>
    public void Toggle() => IsPaused = !IsPaused;

    /// <summary>
    /// Returns zero while paused, and the unmodified frame time while running.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>The frame time that the game should advance by.</returns>
    public TimeSpan Filter(TimeSpan frameTime) => IsPaused ? TimeSpan.Zero : frameTime;
}
