namespace OliveGameStudio;

/// <summary>
/// Provides a contract for objects that can be activated and deactivated.
/// This interface defines methods to manage the lifecycle transitions of an activatable object.
/// </summary>
public interface IActivatable
{
    /// <summary>
    /// Enters the current state or screen of the application.
    /// Responsible for handling the initialization, resource allocation, or state transitions
    /// required when entering the context in which this method is invoked.
    /// Implemented as part of the <see cref="IActivatable"/> interface.
    /// </summary>
    /// <returns>
    /// An <see cref="EnterResult"/> indicating the outcome of the enter operation.
    /// This result can represent staying in the current state or redirecting to another screen.
    /// </returns>
    EnterResult Enter();

    /// <summary>
    /// Exits the current state or screen of the application.
    /// Responsible for handling the cleanup, resource deallocation, or state transitions
    /// required when exiting the context in which this method is invoked.
    /// Implemented as part of the <see cref="IActivatable"/> interface.
    /// </summary>
    void Exit();
}
