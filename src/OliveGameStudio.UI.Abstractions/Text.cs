namespace OliveGameStudio;

/// <summary>
/// Represents a text-based UI element.
/// This sealed class is derived from the Element base type and is used
/// to display or manage textual content in the user interface.
/// </summary>
/// <remarks>
/// An identity, like every <see cref="Element"/>: two labels reading the same words are two
/// things on screen rather than one.
/// </remarks>
/// <param name="content">The textual content of the UI element.</param>
public sealed class Text(string content) : Element
{
    /// <summary>
    /// Gets the textual content of the UI element.
    /// </summary>
    public string Content { get; } = content;
}
