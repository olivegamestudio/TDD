namespace OliveGameStudio;

/// <summary>
/// Represents the main menu screen of the application.
/// This screen serves as the primary user interface for navigating
/// to different parts of the application. It displays menu options
/// and responds to user input for selection.
/// </summary>
/// <remarks>
/// The <see cref="MenuScreen"/> class is responsible for managing
/// and displaying the primary menu interface. It also updates
/// its internal state each frame.
/// </remarks>
public sealed class MenuScreen : IScreen
{
    readonly UIController _controller = new();

    /// <summary>
    /// Represents the main menu screen of the application.
    /// Implements the <see cref="IScreen"/> interface to handle screen updates.
    /// </summary>
    public MenuScreen()
    {
        _controller.Add(new Button());
    }

    /// <summary>
    /// Updates the state of the menu screen. This method is called every frame and
    /// is responsible for processing time-dependent or frame-based logic tied to the menu,
    /// such as animations, logic transitions, or input handling.
    /// </summary>
    /// <param name="frameTime">
    /// The amount of time that has elapsed since the last frame. This is used to
    /// perform time-based updates and ensure consistent behavior across varying frame rates.
    /// </param>
    public void Update(TimeSpan frameTime)
    {
    }
}
