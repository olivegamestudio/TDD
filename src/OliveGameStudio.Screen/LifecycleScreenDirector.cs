using Microsoft.Extensions.Logging;

namespace OliveGameStudio;

/// <summary>
/// Provides a concrete implementation of the <see cref="IScreenDirector"/> interface, enabling
/// management and navigation between screens in an application. The <see cref="LifecycleScreenDirector"/>
/// maintains the current active screen and facilitates its updates and transitions.
/// </summary>
public sealed class LifecycleScreenDirector(ILogger<LifecycleScreenDirector> logger) : IScreenDirector
{
    /// <inheritdoc />
    public IScreen? Current { get; private set; }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        Current?.Update(frameTime);
    }

    /// <inheritdoc />
    public void Draw(IRenderer renderer)
    {
        (Current as IRenderable)?.Render(renderer);
    }

    /// <summary>
    /// Exits the current active screen if it implements <see cref="IActivatable"/>.
    /// This method invokes the <see cref="IActivatable.Exit"/> method on the current screen,
    /// allowing it to perform any necessary cleanup or state transitions before being deactivated.
    /// </summary>
    static void Exit(IScreen? screen) => (screen as IActivatable)?.Exit();

    /// <summary>
    /// Attempts to enter the specified screen by invoking its activation logic.
    /// If the screen implements <see cref="IActivatable"/>, its <see cref="IActivatable.Enter"/> method is called.
    /// </summary>
    /// <param name="screen">The screen to be entered. Must not be <c>null</c>.</param>
    /// <returns>
    /// An <see cref="EnterResult"/> indicating the result of the enter operation.
    /// This result can represent staying on the current screen or redirecting to another screen.
    /// </returns>
    static EnterResult Enter(IScreen screen) =>
        (screen as IActivatable)?.Enter() ?? EnterResult.Stay;
    
    /// <inheritdoc />
    public void NavigateTo(IScreen screen)
    {
        IScreen? next = screen;
        while (next is not null)
        {
            Exit(Current);
            Current = next;
            next = Enter(Current) is EnterResult.Redirect redirect ? redirect.Screen : null;
        }
    }
}
