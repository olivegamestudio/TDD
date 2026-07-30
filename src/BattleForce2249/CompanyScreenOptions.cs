namespace OliveGameStudio;

/// <summary>
/// Configures the company splash screen.
/// </summary>
public sealed class CompanyScreenOptions
{
    /// <summary>
    /// The name of the configuration section these options bind from.
    /// </summary>
    public const string SectionName = "CompanyScreen";

    /// <summary>
    /// How long the splash holds before handing over to the menu. Defaults to two seconds.
    /// Shortening this is the usual reason to configure a debug build differently, so a
    /// developer is not made to sit through the splash.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(2);
}
