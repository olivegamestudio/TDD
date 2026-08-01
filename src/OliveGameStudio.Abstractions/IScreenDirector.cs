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
    /// Draws the current screen, if there is one and it has anything to draw. A screen draws by
    /// implementing <see cref="IRenderable"/> alongside <see cref="IScreen"/>; one that does not
    /// is skipped, so a screen with no visuals yet costs nothing and needs no empty method.
    /// </summary>
    /// <param name="renderer">The frame's renderer.</param>
    void Draw(IRenderer renderer);

    /// <summary>
    /// Navigates to the specified screen by setting it as the new active screen.
    /// This method updates the current state of the screen director to reflect the provided screen.
    /// </summary>
    /// <param name="screen">
    /// The screen to navigate to. This screen will be set as the current active screen,
    /// replacing any previously active screen.
    /// </param>
    void NavigateTo(IScreen screen);
}
