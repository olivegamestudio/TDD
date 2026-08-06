namespace OliveGameStudio;

/// <summary>
/// Represents a countdown timer initialized with a specified duration.
/// </summary>
public sealed class Countdown(TimeSpan duration)
{
    /// <summary>
    /// How long the countdown was given to run. Held in a field of its own rather than read from
    /// the constructor parameter, so the parameter is captured once rather than both stored and
    /// closed over.
    /// </summary>
    readonly TimeSpan _duration = duration;

    /// <summary>
    /// Represents the remaining time in the countdown.
    /// This field holds the duration yet to elapse until the countdown completes.
    /// </summary>
    TimeSpan _remainingTime = duration;

    /// <summary>
    /// Gets a value indicating whether the countdown has completed.
    /// This property returns true if the internal remaining time reaches or falls below zero; otherwise, false.
    /// </summary>
    public bool IsElapsed => _remainingTime <= TimeSpan.Zero;

    /// <summary>
    /// Gets how long the countdown has left, never less than zero.
    /// </summary>
    /// <remarks>
    /// Clamped rather than reported raw, because a countdown is asked how long is left and the
    /// answer to that is never negative — a tick that overshoots the end has simply finished it.
    /// Reporting the overshoot would make every caller subtract it back out, and one that forgot
    /// would read "less than nothing left" as a duration.
    /// </remarks>
    public TimeSpan Remaining => _remainingTime > TimeSpan.Zero ? _remainingTime : TimeSpan.Zero;

    /// <summary>
    /// Gets how long the countdown was given to run.
    /// </summary>
    /// <remarks>
    /// Exposed so that "how far through am I" can be answered by a caller that was not the one
    /// that constructed it — a screen fading its logo knows the countdown but not the duration it
    /// was built with, and passing the same value to both would be two places to change it.
    /// </remarks>
    public TimeSpan Duration => _duration;

    /// <summary>
    /// Resets the countdown timer to its initial duration.
    /// </summary>
    public void Reset() => _remainingTime = _duration;

    /// <summary>
    /// Decrements the remaining time of the countdown by the specified time interval.
    /// </summary>
    /// <param name="time">The amount of time to decrease from the countdown.</param>
    public void Tick(TimeSpan time) => _remainingTime -= time;
}
