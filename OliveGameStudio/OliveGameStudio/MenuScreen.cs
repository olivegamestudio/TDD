namespace OliveGameStudio;

/// <summary>
/// Represents the main menu screen of the application.
/// This class provides the interface and behavior for managing the application’s
/// initial screen where users can interact with the menu options, such as starting the game.
/// Implements the <see cref="IScreen"/> interface.
/// </summary>
public sealed class MenuScreen : IScreen
{
    readonly IUIController _controller;
    readonly Button _startButton = new("START");
    
    /// <summary>
    /// Event triggered when the player initiates a request to start the game.
    /// </summary>
    /// <remarks>
    /// This event is raised when the "Start Game" button is pressed on the main menu screen,
    /// signaling that the game should transition from the menu state to the gameplay state.
    /// Subscribers to this event should handle the necessary tasks to begin the game,
    /// such as loading game assets, initializing the game environment, or setting up the game state.
    /// </remarks>
    public event EventHandler? StartGameRequested;

    /// <summary>
    /// Represents the main menu screen of the application.
    /// This class provides the interface and behavior for managing the application’s
    /// initial screen where users can interact with the menu options, such as starting the game.
    /// Implements the <see cref="IScreen"/> interface.
    /// </summary>
    public MenuScreen(IUIController controller)
    {
        _controller = controller;
        controller.Add(_startButton);
        controller.OnPressed(_startButton, OnStartPressed);
    }

    /// <summary>
    /// Simulates a button press action in the main menu screen.
    /// This method triggers the associated behavior of the currently focused button,
    /// or the specified button, using the provided <see cref="IUIController"/>.
    /// </summary>
    public void Press()
    {
        _controller.Press();
    }
    
    /// <summary>
    /// Handles the event when the start button is pressed in the menu screen.
    /// Invokes the <see cref="StartGameRequested"/> event to signal that the game
    /// should transition from the menu to the gameplay state.
    /// </summary>
    void OnStartPressed() => StartGameRequested?.Invoke(this, EventArgs.Empty);

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
