namespace OliveGameStudio;

/// <summary>
/// Represents a loaded texture resource with known dimensions. Disposing the
/// texture releases the underlying graphics resources it holds.
/// </summary>
public interface ITexture : IDisposable
{
    /// <summary>
    /// Gets the width of the texture, in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the texture, in pixels.
    /// </summary>
    int Height { get; }
}