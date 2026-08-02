using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace OliveGameStudio;

/// <summary>
/// Reads the game's text out of one JSON file per language.
/// </summary>
/// <remarks>
/// A language is a file named after its culture — <c>en.json</c>, <c>fr.json</c>,
/// <c>pt-BR.json</c> — holding a flat object of key to translated text. Translators work in a plain
/// text editor, a new language is a file dropped into the folder rather than a rebuild, and the
/// source file may carry <c>//</c> notes explaining the context of each string.
/// <para>
/// Look-ups follow the culture's fallback chain a key at a time: the culture itself, then its
/// parents, then the source language. So <c>fr-CA</c> reads the French text, and a language that
/// translates only half its keys shows the source language for the rest rather than a blank or a
/// key.
/// </para>
/// </remarks>
public sealed class JsonTextProvider : ITextProvider
{
    /// <summary>
    /// The language the game is written in, and what every other language falls back to.
    /// </summary>
    public const string DefaultSourceLanguage = "en";

    /// <summary>
    /// Lets the source file carry <c>//</c> notes for translators, which JSON has no other place
    /// for, and forgives a trailing comma after the last string.
    /// </summary>
    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The text of each language exactly as authored, with no fallback applied. Keyed without
    /// regard to case, because a culture asks for <c>pt-BR</c> whatever case the file is named in.
    /// </summary>
    readonly Dictionary<string, IReadOnlyDictionary<string, string>> _languages =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Look-ups already resolved through the fallback chain, so the walk happens once per culture
    /// and key rather than once per frame.
    /// </summary>
    readonly ConcurrentDictionary<(string Culture, string Key), string> _resolved = new();

    /// <summary>
    /// Reads every language file in a folder.
    /// </summary>
    /// <param name="directory">The folder holding the language files.</param>
    /// <param name="sourceLanguage">
    /// The language the game is written in, which anything untranslated falls back to.
    /// </param>
    /// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
    /// <exception cref="FileNotFoundException">The folder has no file for the source language.</exception>
    /// <exception cref="InvalidDataException">A language file is not a flat object of strings.</exception>
    public JsonTextProvider(string directory, string sourceLanguage = DefaultSourceLanguage)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"There is no game text folder at '{directory}'.");
        }

        Directory = directory;
        SourceLanguage = sourceLanguage;

        foreach (string file in System.IO.Directory.EnumerateFiles(directory, "*.json"))
        {
            _languages[Path.GetFileNameWithoutExtension(file)] = Read(file);
        }

        if (!_languages.ContainsKey(sourceLanguage))
        {
            throw new FileNotFoundException(
                $"There is no '{sourceLanguage}.json' in '{directory}', so untranslated text has "
                + "nothing to fall back to.",
                Path.Combine(directory, $"{sourceLanguage}.json"));
        }
    }

    /// <summary>
    /// Gets the folder the language files were read from.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Gets the language the game is written in.
    /// </summary>
    public string SourceLanguage { get; }

    /// <summary>
    /// Gets the languages found, the source language included.
    /// </summary>
    public IReadOnlyCollection<string> Languages => _languages.Keys;

    /// <summary>
    /// Gets the text one language actually authored, with no fallback applied — so a language that
    /// is missing a key reads as missing rather than borrowing the source language's.
    /// </summary>
    /// <param name="language">The language name, such as <c>fr</c> or <c>pt-BR</c>.</param>
    /// <returns>That language's keys and text.</returns>
    /// <exception cref="ArgumentException">There is no file for that language.</exception>
    public IReadOnlyDictionary<string, string> TextIn(string language) =>
        _languages.TryGetValue(language, out IReadOnlyDictionary<string, string>? text)
            ? text
            : throw new ArgumentException($"There is no game text for '{language}'.", nameof(language));

    /// <inheritdoc />
    public string Get(string key) => Get(key, CultureInfo.CurrentUICulture);

    /// <inheritdoc />
    public string Get(string key, CultureInfo culture) =>
        _resolved.GetOrAdd(
            (culture.Name, key),
            entry => Resolve(entry.Key, culture) ?? throw new MissingTextException(entry.Key));

    /// <summary>
    /// Walks the fallback chain for one key: the culture, each of its parents in turn, and finally
    /// the source language.
    /// </summary>
    /// <param name="key">The key to find text for.</param>
    /// <param name="culture">The language to start from.</param>
    /// <returns>The text, or <c>null</c> when no language along the chain has the key.</returns>
    string? Resolve(string key, CultureInfo culture)
    {
        // the invariant culture is named "" and is its own parent, so it ends the walk
        for (CultureInfo candidate = culture; candidate.Name.Length > 0; candidate = candidate.Parent)
        {
            if (_languages.TryGetValue(candidate.Name, out IReadOnlyDictionary<string, string>? text)
                && text.TryGetValue(key, out string? translated))
            {
                return translated;
            }
        }

        return _languages[SourceLanguage].GetValueOrDefault(key);
    }

    /// <summary>
    /// Reads one language file.
    /// </summary>
    /// <param name="file">The file to read.</param>
    /// <returns>The keys and text it holds.</returns>
    /// <exception cref="InvalidDataException">
    /// The file is not a flat JSON object of strings. A broken translation is a content mistake, so
    /// it says which file is at fault rather than failing later as a missing key.
    /// </exception>
    static IReadOnlyDictionary<string, string> Read(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file), _jsonOptions)
                ?? throw new InvalidDataException($"The game text in '{file}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The game text in '{file}' is not a flat JSON object of strings.", exception);
        }
    }
}
