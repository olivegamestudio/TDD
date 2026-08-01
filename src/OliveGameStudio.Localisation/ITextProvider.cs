using System.Globalization;

namespace OliveGameStudio;

/// <summary>
/// Reads the game's user-facing text in the player's language.
/// </summary>
/// <remarks>
/// The engine provides the reading; a game provides the text, exactly as it does for saved
/// progress. Only text the player actually reads belongs behind this — identifiers such as a quest
/// id are not text and must never be translated, because a save written in one language has to
/// load in another.
/// </remarks>
public interface ITextProvider
{
    /// <summary>
    /// Gets a string in the player's language, taken from <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    /// <param name="key">The key, identical across every language.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="MissingTextException">
    /// The key is in no language at all, the source language included.
    /// </exception>
    string Get(string key);

    /// <summary>
    /// Gets a string in a particular language, falling back when that language does not translate
    /// it.
    /// </summary>
    /// <param name="key">The key, identical across every language.</param>
    /// <param name="culture">The language to read in.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="MissingTextException">
    /// The key is in no language at all, the source language included.
    /// </exception>
    string Get(string key, CultureInfo culture);
}
