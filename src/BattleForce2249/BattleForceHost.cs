namespace OliveGameStudio;

/// <summary>
/// Represents the main class for initializing and managing the overall game flow.
/// The <c>Game</c> class is responsible for starting the game, managing screen transitions,
/// and updating the game state based on frame time.
/// </summary>
public class BattleForceHost(IScreenDirector screenDirector, IUIController controller, ISaveProgressService saveProgress) : IHost
{
    readonly CompanyScreen _companyScreen = new(TimeSpan.FromSeconds(2));
    readonly MenuScreen _menuScreen = new(controller, saveProgress);
    readonly GameScreen _gameScreen = new();

    /// <summary>
    /// Starts the Game by navigating to the initial screen (CompanyScreen).
    /// Once the CompanyScreen completes, it transitions to the MenuScreen.
    /// </summary>
    public void Start()
    {
        _companyScreen.Completed += (_, _) => screenDirector.NavigateTo(_menuScreen);
        screenDirector.NavigateTo(_companyScreen);
        
        _menuScreen.StartGameRequested += (_, _) => screenDirector.NavigateTo(_gameScreen);
    }

    /// <summary>
    /// Updates the state of the game and its current screen based on the elapsed frame time.
    /// This method is responsible for delegating the update logic to the screen director which
    /// manages the currently active screen.
    /// </summary>
    /// <param name="frameTime">
    /// The time that has elapsed since the last frame. This value is used to calculate
    /// updates in game logic, animations, and other time-dependent processes.
    /// </param>
    public void Update(TimeSpan frameTime)
    {
        screenDirector.Update(frameTime);
    }
}
