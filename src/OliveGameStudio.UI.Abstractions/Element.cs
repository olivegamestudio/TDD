namespace OliveGameStudio;

/// <summary>
/// Represents the base type for all UI components.
/// This is an abstract class that serves as a foundation for specific UI elements
/// such as buttons, images, and other interactive or non-interactive components.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elements are identities, not values.</b> Two elements are the same element only when they
/// are the same object; nothing about them is compared. This is why the hierarchy is classes
/// rather than records.
/// </para>
/// <para>
/// It matters most for <see cref="Button"/>, which the UI controller resolves to a node on every
/// call. As records, two buttons that happened to share a name were indistinguishable, and since
/// the controller is a singleton shared by every screen, one screen's <c>BACK</c> silently
/// disabled another screen's <c>BACK</c>. The same reasoning holds for the rest: two images of
/// the same asset are two things on screen, not one.
/// </para>
/// </remarks>
public abstract class Element
{
}
