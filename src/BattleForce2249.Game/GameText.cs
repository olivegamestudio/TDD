using System.Globalization;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The game's user-facing text, in the player's language.
/// </summary>
/// <remarks>
/// Text lives in <c>Text/en.json</c> (English, the source translators work from) with one
/// <c>Text/&lt;culture&gt;.json</c> per translation beside it, copied next to the game. Look-ups
/// follow <see cref="CultureInfo.CurrentUICulture"/> and fall back to English when a culture has no
/// translation, so an untranslated string still shows readable text rather than a blank or a key.
/// <para>
/// Only text the player actually reads belongs here. Identifiers such as
/// <see cref="BattleForceCampaign.Quest1Id"/> are not text and must never be translated — a save
/// game written in one language has to load in another.
/// </para>
/// </remarks>
public static class GameText
{
    /// <summary>
    /// The folder the language files are copied into, beside the game.
    /// </summary>
    public const string TextFolderName = "Text";

    /// <summary>
    /// Reads the language files. Deferred until the first string is asked for, so the files are
    /// read once and a missing folder fails where the text is used rather than as a type
    /// initializer error somewhere unrelated.
    /// </summary>
    static readonly Lazy<ITextProvider> _text =
        new(() => new JsonTextProvider(Path.Combine(AppContext.BaseDirectory, TextFolderName)));

    /// <summary>
    /// Gets the provider the text is read through.
    /// </summary>
    public static ITextProvider Provider => _text.Value;

    /// <summary>
    /// Gets the title of quest 1, as shown to the player.
    /// </summary>
    public static string Quest1Title => Get(nameof(Quest1Title));

    /// <summary>
    /// Gets a string in the player's language, falling back to English when the current culture has
    /// no translation of it.
    /// </summary>
    /// <param name="key">The key, identical across every language.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="MissingTextException">
    /// The key is in no language at all, English included. That is a mistake in the game rather
    /// than a missing translation, so it fails loudly instead of showing the player the key.
    /// </exception>
    public static string Get(string key) => Provider.Get(key);
}
