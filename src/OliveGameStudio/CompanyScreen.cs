namespace OliveGameStudio;

/// <summary>
/// Represents the initial screen displayed in the application, typically shown for a fixed duration.
/// Implements the <see cref="IScreen"/> interface.
/// </summary>
public sealed class CompanyScreen(TimeSpan duration) : IScreen
{
    /// <summary>
    /// Manages the countdown timer responsible for determining the duration of the current screen.
    /// Tracks the remaining time until the screen's completion and allows the application
    /// to advance to the next stage once the countdown elapses.
    /// </summary>
    readonly Countdown _countdown = new(duration);

    /// <summary>
    /// Indicates whether the associated screen has finished its operation or transition.
    /// This flag is set to true once the countdown duration elapses, signaling that
    /// the screen's lifecycle is complete and the <see cref="CompanyScreen.Completed"/> event is raised.
    /// </summary>
    bool _hasCompleted;

    /// <summary>
    /// Occurs when the associated process, operation, or screen transition is completed.
    /// The event signifies the end of a specific activity or timer within the application.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Updates the state of the screen based on the time elapsed since the last frame.
    /// This method progresses the countdown timer. If the timer has elapsed, it marks the screen
    /// as completed and raises the <see cref="Completed"/> event.
    /// </summary>
    /// <param name="frameTime">The duration of time that has passed since the last update.</param>
    public void Update(TimeSpan frameTime)
    {
        if (_hasCompleted)
        {
            return;
        }
        
        _countdown.Tick(frameTime);
        if (!_countdown.IsElapsed)
        {
            return;
        }
        
        _hasCompleted = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
