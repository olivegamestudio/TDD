namespace OliveGameStudio;

/// <summary>
/// Represents a countdown timer initialized with a specified duration.
/// </summary>
public sealed class Countdown
{
    /// <summary>
    /// Creates a countdown that starts with its whole duration left to run.
    /// </summary>
    /// <param name="duration">
    /// How long the countdown is given to run. Zero is allowed and means a countdown that is
    /// finished the moment it is made — a screen configured to last no time skipping itself, not a
    /// mistake.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The duration is negative. A countdown built with one starts elapsed and can never be
    /// rearmed, because <see cref="Reset"/> puts the same negative value back — it would fail the
    /// one promise this type makes about resetting, and would do it silently, as a screen that
    /// never appears rather than as an error anyone could read.
    /// </exception>
    public Countdown(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        _duration = duration;
        _remainingTime = duration;
    }

    /// <summary>
    /// How long the countdown was given to run. Held in a field of its own rather than read from
    /// the constructor parameter, so the parameter is captured once rather than both stored and
    /// closed over.
    /// </summary>
    readonly TimeSpan _duration;

    /// <summary>
    /// Represents the remaining time in the countdown.
    /// This field holds the duration yet to elapse until the countdown completes.
    /// </summary>
    TimeSpan _remainingTime;

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
    /// <remarks>
    /// Rearms whatever state the countdown was in, which is only true because the constructor
    /// refuses a negative duration — putting one back would leave the countdown elapsed and no
    /// amount of resetting would ever start it again.
    /// </remarks>
    public void Reset() => _remainingTime = _duration;

    /// <summary>
    /// Decrements the remaining time of the countdown by the specified time interval.
    /// </summary>
    /// <param name="time">
    /// How much time has passed. Zero is allowed and does nothing — a frame that took no
    /// measurable time is a real frame, and refusing it would make the game loop check the clock
    /// before it was allowed to report it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The time is negative. Ticking backwards is not slowing a countdown down, it is un-elapsing
    /// one: <see cref="IsElapsed"/> is meant to go one way and only <see cref="Reset"/> is meant to
    /// bring it back, so a negative tick would rearm a countdown behind the back of the screen
    /// that owns it. The same mistake <c>Meter.Reduce</c> refuses, and refused for the same reason
    /// — a duration that came out backwards should be read as an error, not as time returned.
    /// </exception>
    public void Tick(TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(time, TimeSpan.Zero);

        _remainingTime -= time;
    }
}
