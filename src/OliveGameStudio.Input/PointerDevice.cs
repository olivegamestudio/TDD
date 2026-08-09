namespace OliveGameStudio;

/// <summary>
/// Which kind of device put a pointer on the screen.
/// </summary>
/// <remarks>
/// Carried only so that two pointers can be told apart. A mouse and a finger both report a number
/// as their identifier and the two numbering schemes have nothing to do with each other, so
/// without this a finger the platform called 0 and the mouse would be the same pointer. Nothing
/// downstream is meant to <em>behave</em> differently on the strength of it — a drag reads the same
/// whichever device is doing it, which is the whole reason <c>Pointer</c> exists.
/// </remarks>
public enum PointerDevice
{
    /// <summary>
    /// The mouse. There is only ever one, so its identifier is always zero.
    /// </summary>
    Mouse,

    /// <summary>
    /// A finger on the touch screen, identified by the number the platform gave it.
    /// </summary>
    Touch,
}
