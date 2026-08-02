namespace OliveGameStudio;

/// <summary>
/// The outcome of entering a screen: either the screen keeps the navigation, or it hands the
/// navigation on to another screen.
/// </summary>
/// <remarks>
/// A screen decides this in its own <see cref="IActivatable.Enter"/>, in isolation from the screens
/// around it, which is why the director bounds the chain rather than trusting it to end.
/// </remarks>
public abstract record EnterResult
{
    /// <summary>
    /// The screen keeps the navigation and is left on display. This is also what a screen that does
    /// not implement <see cref="IActivatable"/> is taken to mean.
    /// </summary>
    public static readonly EnterResult Stay = new StayResult();

    /// <summary>
    /// Hands the navigation on to <paramref name="screen"/>, which is entered in turn.
    /// </summary>
    /// <param name="screen">The screen to enter instead of the one that returned this result.</param>
    /// <remarks>
    /// The redirect target may itself redirect, so a navigation can pass through several screens.
    /// It may not come back to one already entered during the same navigation — a director that
    /// follows redirects rejects the repeat rather than following it round for ever. See
    /// <see cref="IScreenDirector.NavigateTo"/>.
    /// </remarks>
    public static EnterResult RedirectTo(IScreen screen) => new Redirect(screen);

    /// <summary>
    /// The result of a screen that keeps the navigation. Obtained through <see cref="Stay"/>.
    /// </summary>
    public sealed record StayResult : EnterResult;

    /// <summary>
    /// The result of a screen that hands the navigation to another. Obtained through
    /// <see cref="RedirectTo"/>.
    /// </summary>
    /// <param name="Screen">The screen to enter instead.</param>
    public sealed record Redirect(IScreen Screen) : EnterResult;
}
