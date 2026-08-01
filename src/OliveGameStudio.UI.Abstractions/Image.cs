namespace OliveGameStudio;

/// <summary>
/// Represents an image component in the UI.
/// This class extends the functionality of the base Element class
/// and is used for displaying visual content.
/// </summary>
/// <remarks>
/// An identity, like every <see cref="Element"/>: two images of the same asset are two things on
/// screen rather than one.
/// </remarks>
/// <param name="name">The asset key the image is drawn from.</param>
public sealed class Image(string name) : Element
{
    /// <summary>
    /// Gets the asset key the image is drawn from. An identifier, never translated.
    /// </summary>
    public string Name { get; } = name;
}
