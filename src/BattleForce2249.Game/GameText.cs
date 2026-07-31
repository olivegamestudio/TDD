using System.Globalization;
using System.Resources;

// The neutral GameText.resx holds the English source text, so English needs no satellite assembly
// and is what any culture without a translation falls back to.
[assembly: NeutralResourcesLanguage("en")]

namespace BattleForce2249;

/// <summary>
/// The game's user-facing text, in the player's language.
/// </summary>
/// <remarks>
/// Text lives in <c>GameText.resx</c> (English, the source translators work from) with one
/// <c>GameText.&lt;culture&gt;.resx</c> per translation beside it. Look-ups follow
/// <see cref="CultureInfo.CurrentUICulture"/> and fall back to English when a culture has no
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
    /// Reads the strings out of the resx family. Constructed from <see cref="GameText"/> itself so
    /// the resource base name always matches the type, whichever way the build names the resources.
    /// </summary>
    static readonly ResourceManager _resources = new(typeof(GameText));

    /// <summary>
    /// Gets the title of quest 1, as shown to the player.
    /// </summary>
    public static string Quest1Title => Get(nameof(Quest1Title));

    /// <summary>
    /// Gets a string in the player's language, falling back to English when the current culture has
    /// no translation of it.
    /// </summary>
    /// <param name="key">The resource key, identical across every language.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="MissingManifestResourceException">
    /// The key is in no resource file at all, English included. That is a mistake in the game rather
    /// than a missing translation, so it fails loudly instead of showing the player the key.
    /// </exception>
    public static string Get(string key) =>
        _resources.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException(
            $"There is no game text with the key '{key}' in any language, English included.");
}
