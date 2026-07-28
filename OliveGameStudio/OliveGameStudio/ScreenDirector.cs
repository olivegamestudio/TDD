namespace OliveGameStudio;

/// <summary>
/// A concrete implementation of the <see cref="IScreenDirector"/> interface that manages and navigates between screens within an application.
/// This class maintains the currently active screen and provides methods to update its state
/// and transition to other screens.
/// </summary>
public sealed class ScreenDirector : IScreenDirector
{
    /// <inheritdoc />
    public IScreen? Current { get; private set; }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        Current?.Update(frameTime);
    }

    /// <inheritdoc />
    public void NavigateTo(IScreen screen)
    {
        Current = screen;
    }
}
