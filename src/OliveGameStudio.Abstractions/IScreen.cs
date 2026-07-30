namespace OliveGameStudio;

/// <summary>
/// Defines the contract for a screen within the application framework.
/// Screens implementing this interface should handle their updates according to the frame time provided.
/// </summary>
public interface IScreen
{
    /// <summary>
    /// Updates the state of the screen.
    /// This method is called on every frame and is responsible for handling
    /// the logic and transitions of the screen during the application's lifecycle.
    /// </summary>
    /// <param name="frameTime">
    /// The time that has elapsed since the last frame.
    /// This value is used to perform time-based updates and animations.
    /// </param>
    void Update(TimeSpan frameTime);
}