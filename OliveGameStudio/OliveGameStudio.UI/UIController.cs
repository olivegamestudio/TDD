namespace OliveGameStudio;

/// <summary>
/// Manages the user interface (UI) elements and their interactions.
/// It provides functionality to add elements, manage focus on specific elements,
/// and handle events like pressing a button or other interactions.
/// </summary>
public sealed class UIController
{
    readonly List<Element> _elements = [];
    
    Button? _focusedElement;

    /// <summary>
    /// Occurs when a "press" action is triggered on the currently focused user interface element.
    /// This event is invoked by the <see cref="UIController.Press"/> method when a focused element,
    /// such as a button, is active and a press action is performed.
    /// </summary>
    /// <remarks>
    /// If no element is focused in the <see cref="UIController"/>, the event will not be invoked.
    /// Subscribers can use this event to handle actions triggered by user interaction with UI components.
    /// </remarks>
    public event EventHandler<PressedEventArgs>? Pressed;

    /// <summary>
    /// Simulates a "press" action on the currently focused user interface element.
    /// If a button is currently focused, the method triggers the <see cref="Pressed"/> event.
    /// If no element is focused, no action is performed.
    /// </summary>
    /// <remarks>
    /// This method ensures that only the element currently set as focused receives the "press" action.
    /// </remarks>
    public void Press()
    {
        if (_focusedElement is not null)
        {
            Pressed?.Invoke(this, new PressedEventArgs(_focusedElement));
        }
    }

    /// <summary>
    /// Adds a new UI element to the internal collection managed by the controller.
    /// This allows the element to participate in UI interactions such as receiving focus or being pressed.
    /// </summary>
    /// <param name="element">The UI element to be added to the controller.</param>
    public void Add(Element element)
    {
        _elements.Add(element);

        if (_focusedElement is null && element is Button button)
        {
            _focusedElement = button;
        }
    }

    /// <summary>
    /// Sets focus on the specified button, making it the currently active UI element
    /// within the controller.
    /// </summary>
    /// <param name="start">The <see cref="Button"/> instance to be focused.</param>
    public void FocusOn(Button start)
    {
        _focusedElement = start;
    }
}
