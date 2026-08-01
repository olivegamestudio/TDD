namespace OliveGameStudio;

/// <summary>
/// Turns an asset key into a texture the platform can draw. The engine names what it wants to
/// draw; where the bytes come from and how they reach the graphics device is the platform's
/// business, which keeps everything above this seam testable without a rendering framework.
/// </summary>
/// <remarks>
/// Asset keys are identifiers, not text. They are never translated, because a build that
/// renamed its assets per language could not load a save — or a screen — written by another.
/// </remarks>
public interface ITextureLoader
{
    /// <summary>
    /// Loads the texture stored under <paramref name="key"/>, returning the same instance for
    /// the same key so callers can load on first use rather than holding a preload phase open.
    /// </summary>
    /// <param name="key">The asset key, as authored in the content build.</param>
    /// <returns>The loaded texture. The loader owns its lifetime.</returns>
    ITexture Load(string key);
}
