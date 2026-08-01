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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the redirect chain re-enters a screen it has already entered during this
    /// navigation. Redirect targets come from game code, so two screens that each redirect to
    /// the other — or a screen that redirects to itself — would otherwise spin forever inside
    /// this call, freezing the update loop with no diagnostic. A screen may be navigated to
    /// again later; the restriction is one entry per screen per navigation.
    /// </exception>
    /// <remarks>
    /// When the cycle is detected the navigation is abandoned: the screen entered last is
    /// exited and <see cref="Current"/> is left as <c>null</c>, so no screen is left believing
    /// it is on display and <see cref="Update"/> ticks nothing.
    /// </remarks>
    public void NavigateTo(IScreen screen)
    {
        // Ordered rather than a set, because the message has to name the path that got here.
        // Chains are a handful of screens long, so the linear scan costs nothing.
        List<IScreen> entered = [];

        IScreen? next = screen;
        while (next is not null)
        {
            if (AlreadyEntered(entered, next))
            {
                // Current was entered and never exited, so it is still live. Exit it before
                // abandoning the navigation, or it is left thinking it is on display.
                Exit(Current);
                Current = null;

                throw new InvalidOperationException(
                    $"Redirect cycle while navigating: {Describe([.. entered, next])}. " +
                    "A screen may not be entered twice during one navigation.");
            }

            entered.Add(next);
            Exit(Current);
            Current = next;
            next = Enter(Current) is EnterResult.Redirect redirect ? redirect.Screen : null;
        }
    }

    /// <summary>
    /// Determines whether the specified screen has already been entered during this navigation.
    /// </summary>
    /// <remarks>
    /// Compares by reference deliberately: two instances of the same screen type are two
    /// different screens, and a screen implemented as a record would otherwise collide with
    /// any equal-valued sibling.
    /// </remarks>
    static bool AlreadyEntered(List<IScreen> entered, IScreen screen)
    {
        foreach (IScreen candidate in entered)
        {
            if (ReferenceEquals(candidate, screen))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Renders a redirect path as screen type names, for the cycle diagnostic.
    /// </summary>
    static string Describe(IEnumerable<IScreen> path) =>
        string.Join(" -> ", path.Select(screen => screen.GetType()));
}
