namespace OliveGameStudio;

/// <summary>
/// Provides a concrete implementation of the <see cref="IScreenDirector"/> interface, enabling
/// management and navigation between screens in an application. The <see cref="LifecycleScreenDirector"/>
/// maintains the current active screen and facilitates its updates and transitions.
/// </summary>
public class LifecycleScreenDirector : IScreenDirector
{
    /// <inheritdoc />
    public IScreen? Current { get; private set; }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        Current?.Update(frameTime);
    }

    /// <summary>
    /// Exits the current active screen if it implements <see cref="IActivatable"/>.
    /// This method invokes the <see cref="IActivatable.Exit"/> method on the current screen,
    /// allowing it to perform any necessary cleanup or state transitions before being deactivated.
    /// </summary>
    void ExitCurrent()
    {
        if (Current is IActivatable currentActivatable)
        {
            currentActivatable.Exit();
        }
    }

    /// <summary>
    /// Activates the current screen, if it implements the <see cref="IActivatable"/> interface.
    /// Calls the <see cref="IActivatable.Enter"/> method on the current screen to handle
    /// any initialization logic necessary when the screen becomes active.
    /// </summary>
    void EnterCurrent()
    {
        if (Current is IActivatable activatable)
        {
            activatable.Enter();
        }
    }
    
    /// <inheritdoc />
    public void NavigateTo(IScreen screen)
    {
        ExitCurrent();
        Current = screen;
        EnterCurrent();
    }
}
