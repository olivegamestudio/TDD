using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Represents the main class for initializing and managing the overall game flow.
/// The <c>Game</c> class is responsible for starting the game, managing screen transitions,
/// and updating the game state based on frame time.
/// </summary>
public class BattleForceHost(
    IScreenDirector screenDirector,
    IFrameTimeController frameTimeController,
    ICompanyScreen companyScreen,
    IMenuScreen menuScreen,
    IGameScreen gameScreen) : IHost
{
    /// <summary>
    /// Starts the Game by navigating to the initial screen (CompanyScreen).
    /// Once the CompanyScreen completes, it transitions to the MenuScreen.
    /// </summary>
    public void Start()
    {
        companyScreen.Completed += (_, _) => screenDirector.NavigateTo(menuScreen);
        screenDirector.NavigateTo(companyScreen);
        
        menuScreen.StartGameRequested += (_, _) => screenDirector.NavigateTo(gameScreen);
    }

    /// <summary>
    /// Updates the state of the game and its current screen based on the elapsed frame time.
    /// The frame time is first passed through the <see cref="IFrameTimeController"/>, so that a
    /// paused or slowed game holds the active screen still while frames keep arriving. The
    /// filtered result is then delegated to the screen director which manages the active screen.
    /// </summary>
    /// <param name="frameTime">
    /// The real time that has elapsed since the last frame. This value is used to calculate
    /// updates in game logic, animations, and other time-dependent processes.
    /// </param>
    public void Update(TimeSpan frameTime)
    {
        screenDirector.Update(frameTimeController.Filter(frameTime));
    }

    /// <summary>
    /// Draws the current screen. The frame time controller has no part in this: pausing the
    /// game stops it advancing, and a paused game is still on screen.
    /// </summary>
    /// <param name="renderer">The frame's renderer, owned by the platform host.</param>
    public void Draw(IRenderer renderer)
    {
        screenDirector.Draw(renderer);
    }
}
