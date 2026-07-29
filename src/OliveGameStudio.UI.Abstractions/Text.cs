namespace OliveGameStudio;

/// <summary>
/// Represents a text-based UI element.
/// This sealed record is derived from the Element base type and is used
/// to display or manage textual content in the user interface.
/// </summary>
/// <param name="Content">The textual content of the UI element.</param>
public sealed record Text(string Content) : Element;
