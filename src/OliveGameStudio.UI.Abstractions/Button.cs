namespace OliveGameStudio;

/// <summary>
/// Represents a UI button component in the system.
/// </summary>
/// <remarks>
/// The <see cref="Button"/> class is part of the UI element hierarchy and inherits from the <see cref="Element"/> class.
/// This class encapsulates the name of the button, which can be used for identification or display purposes within a user interface.
/// <para>
/// A button is an identity: two buttons are the same button only when they are the same object.
/// The name labels it, it does not identify it — see <see cref="Element"/> for why that
/// distinction is load bearing.
/// </para>
/// </remarks>
/// <param name="name">The name the button is labelled with.</param>
public sealed class Button(string name) : Element
{
    /// <summary>
    /// Gets the button's name. A label, not an identifier: two buttons may share one, and the
    /// controller still treats them as two buttons.
    /// </summary>
    public string Name { get; } = name;
}
