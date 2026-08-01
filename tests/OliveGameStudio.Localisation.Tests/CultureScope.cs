using System.Globalization;

namespace OliveGameStudio.Localisation.Tests;

/// <summary>
/// Runs part of a test as though the player had chosen a particular language, and puts the previous
/// one back afterwards so the choice cannot leak into another test.
/// </summary>
/// <remarks>
/// <see cref="CultureInfo.CurrentUICulture"/> is per thread, so tests running in parallel do not see
/// each other's language.
/// </remarks>
sealed class CultureScope : IDisposable
{
    readonly CultureInfo _previous;

    /// <summary>
    /// Switches the current thread to <paramref name="culture"/> until the scope is disposed.
    /// </summary>
    /// <param name="culture">The culture name to run under, such as <c>fr</c> or <c>pt-BR</c>.</param>
    public CultureScope(string culture)
    {
        _previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
    }

    /// <summary>
    /// Restores the language that was in force before the scope began.
    /// </summary>
    public void Dispose() => CultureInfo.CurrentUICulture = _previous;
}
