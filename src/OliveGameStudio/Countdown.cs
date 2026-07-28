namespace OliveGameStudio;

/// <summary>
/// Represents a countdown timer initialized with a specified duration.
/// </summary>
public sealed class Countdown(TimeSpan duration)
{
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
    /// Resets the countdown timer to its initial duration.
    /// </summary>
    public void Reset() => _remainingTime = duration;

    /// <summary>
    /// Decrements the remaining time of the countdown by the specified time interval.
    /// </summary>
    /// <param name="time">The amount of time to decrease from the countdown.</param>
    public void Tick(TimeSpan time) => _remainingTime -= time;
}
