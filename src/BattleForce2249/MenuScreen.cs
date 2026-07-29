namespace OliveGameStudio;

/// <summary>
/// Represents the main menu screen of the application.
/// This class provides the interface and behavior for managing the application’s
/// initial screen where users can interact with the menu options, such as starting the game.
/// Implements the <see cref="IScreen"/> interface.
/// </summary>
public sealed class MenuScreen : IScreen, IActivatable
{
    readonly IUIController _controller;
    readonly ISaveProgressService _saveProgressService;
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
    public MenuScreen(IUIController controller, ISaveProgressService saveProgressService)
    {
        _controller = controller;
        _saveProgressService = saveProgressService;

        // button is initially disabled until the save progress service has determined the player state.
        controller.Add(_startButton);
        controller.Disable(_startButton);
        
        // begin game if start button is released
        controller.OnReleased(_startButton, OnStartReleased);
    }

    /// <summary>
    /// Simulates a button press action in the main menu screen.
    /// This method triggers the associated behavior of the currently focused button,
    /// or the specified button, using the provided <see cref="IUIController"/>.
    /// </summary>
    public void Press() => _controller.Press();

    /// <summary>
    /// Releases the currently pressed button associated with the main menu screen.
    /// This method interacts with the <see cref="IUIController"/> to stop the press action
    /// and execute the associated functionality of the button if it is enabled.
    /// </summary>
    public void Release() => _controller.Release();
    
    /// <summary>
    /// Handles the event when the start button is pressed in the menu screen.
    /// Invokes the <see cref="StartGameRequested"/> event to signal that the game
    /// should transition from the menu to the gameplay state.
    /// </summary>
    void OnStartReleased()
    {
        // immediately disable to start button to prevent further presses
        // whilst we wait for screen transition/progressing
        _controller.Disable(_startButton);
        
        // request to start game - new one or continuation
        StartGameRequested?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Activates and transitions into the main menu screen of the application.
    /// This method is responsible for setting up the initial state of the menu,
    /// including focusing on the primary UI elements such as the start button.
    /// Implements the <see cref="IActivatable"/> interface.
    /// </summary>
    public EnterResult Enter()
    {
        IsReadyForInput = false;
        _controller.Disable(_startButton);
        _saveProgressTask = HasSaveProgress();
        return EnterResult.Stay;
    }

    /// <summary>
    /// Indicates whether the menu screen is ready to accept user input.
    /// </summary>
    /// <remarks>
    /// This property becomes <c>true</c> once the necessary prerequisites, such as verifying
    /// the player's save progress, are completed and the user interface elements (e.g., buttons)
    /// are enabled. It remains <c>false</c> when the menu screen is initializing or transitioning
    /// into its active state. This flag helps ensure user interactions are processed only when
    /// the menu screen is fully prepared.
    /// </remarks>
    public bool IsReadyForInput { get; private set; }

    /// <summary>
    /// Represents a task responsible for managing the progress of saving the player's game state.
    /// </summary>
    /// <remarks>
    /// This task is utilized to handle the asynchronous operation of saving player data, initiated during
    /// specific interactions within the main menu screen. The task ensures that the save process
    /// is performed without blocking the user interface, maintaining a seamless user experience.
    /// </remarks>
    Task _saveProgressTask;

    /// <summary>
    /// Determines whether the save progress exists in the system.
    /// This method utilises the <see cref="ISaveProgressService"/> to check for the presence
    /// of saved progress data asynchronously. Once the check is complete, the method enables
    /// the start button for user interaction and sets the menu state to ready for input.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The task's result is true if save
    /// progress exists; otherwise, false.
    /// </returns>
    async Task HasSaveProgress()
    {
        bool result = await _saveProgressService.HasProgress();
        _controller.Enable(_startButton);
        IsReadyForInput = true;
    }

    /// <summary>
    /// Exits the main menu screen of the application.
    /// This method is responsible for handling the necessary cleanup and state transitions
    /// when leaving the main menu. Implements the <see cref="IActivatable"/> interface.
    /// </summary>
    public void Exit()
    {
    }
}
