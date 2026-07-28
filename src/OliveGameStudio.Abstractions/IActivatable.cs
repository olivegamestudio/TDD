namespace OliveGameStudio;

/// <summary>
/// Provides a contract for objects that can be activated and deactivated.
/// This interface defines methods to manage the lifecycle transitions of an activatable object.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// Activates and transitions into the main menu screen of the application.
    /// This method is responsible for initializing and setting up the
    /// main menu screen by focusing on the primary user interface elements,
    /// such as the start button.
    /// </summary>
    void Enter();

    /// <summary>
    /// Exits the current state or screen of the application.
    /// Responsible for handling the cleanup, resource deallocation, or state transitions
    /// required when exiting the context in which this method is invoked.
    /// Implemented as part of the <see cref="IActivatable"/> interface.
    /// </summary>
    void Exit();
}
