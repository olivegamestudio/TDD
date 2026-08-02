using Microsoft.Extensions.Logging;

namespace OliveGameStudio;

/// <summary>
/// A concrete implementation of the <see cref="IScreenDirector"/> interface that manages and navigates between screens within an application.
/// This class maintains the currently active screen and provides methods to update its state
/// and transition to other screens.
/// </summary>
public sealed class ScreenDirector(ILogger<ScreenDirector> logger) : IScreenDirector
{
    /// <inheritdoc />
    public IScreen? Current { get; private set; }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        Current?.Update(frameTime);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This director does not enter the screen, so it never reads an <see cref="EnterResult"/> and
    /// a screen asking to redirect is simply not heard. The one-entry-per-navigation rule the
    /// interface states therefore cannot be reached here — there is no chain to bound. A game that
    /// wants a redirect honoured wants <see cref="LifecycleScreenDirector"/> instead.
    /// </remarks>
    public void NavigateTo(IScreen screen)
    {
        logger.LogInformation("Navigate to '{screenName}'.", screen.GetType());
        Current = screen;
    }
}
