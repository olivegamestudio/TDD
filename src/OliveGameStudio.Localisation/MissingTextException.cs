namespace OliveGameStudio;

/// <summary>
/// Thrown when a key has no text in any language, the source language included.
/// </summary>
/// <remarks>
/// This is a mistake in the game rather than a missing translation — a language that has simply not
/// been translated falls back instead — so it fails loudly rather than showing the player a key.
/// </remarks>
public sealed class MissingTextException : Exception
{
    /// <summary>
    /// Creates the exception for a key that no language defines.
    /// </summary>
    /// <param name="key">The key that was asked for.</param>
    public MissingTextException(string key)
        : base($"There is no game text with the key '{key}' in any language, the source language included.")
    {
        Key = key;
    }

    /// <summary>
    /// Gets the key that had no text.
    /// </summary>
    public string Key { get; }
}
