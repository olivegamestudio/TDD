namespace OliveGameStudio;

/// <summary>
/// Represents a UI button component in the system.
/// </summary>
/// <remarks>
/// The <see cref="Button"/> class is part of the UI element hierarchy and inherits from the <see cref="Element"/> class.
/// This class encapsulates the name of the button, which can be used for identification or display purposes within a user interface.
/// </remarks>
public sealed record Button(string Name) : Element;
