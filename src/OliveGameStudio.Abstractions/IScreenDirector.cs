namespace OliveGameStudio;

/// <summary>
/// Represents a director for managing and navigating between screens in an application.
/// This interface provides functionality to track the current screen,
/// update the screen's state, and transition to a new screen.
/// </summary>
public interface IScreenDirector
{
    /// <summary>
    /// Gets the currently active IScreen instance managed by the IScreenDirector.
    /// This property reflects the screen that is presently being displayed and updated within the application,
    /// enabling navigation and state management across different screens. Returns null if no screen is set.
    /// </summary>
    IScreen? Current { get; }

    /// <summary>
    /// Updates the current screen. This method is called on every frame during the application's lifecycle
    /// and is responsible for delegating the update logic to the currently active screen, if any.
    /// </summary>
    /// <param name="frameTime">
    /// The time that has elapsed since the last frame.
    /// This value is used to update the state of the active screen.
    /// 
    /// If no screen is currently active (`Current` is null), this method does nothing.
    /// </param>
    void Update(TimeSpan frameTime);

    /// <summary>
    /// Navigates to the specified screen by setting it as the new active screen.
    /// This method updates the current state of the screen director to reflect the provided screen.
    /// </summary>
    /// <param name="screen">
    /// The screen to navigate to. This screen will be set as the current active screen,
    /// replacing any previously active screen.
    /// </param>
    /// <remarks>
    /// An implementation that honours <see cref="EnterResult.Redirect"/> keeps entering screens
    /// until one asks to stay, so a single call may pass through several screens before it settles.
    /// A screen entered along that chain may not be entered twice within the one call: the redirect
    /// targets come from game code, and a chain that comes back on itself would otherwise never
    /// return, freezing the update loop. Navigating to a screen visited by an earlier call is
    /// ordinary and unrestricted — the rule is one entry per screen per navigation.
    /// </remarks>
    void NavigateTo(IScreen screen);
}
