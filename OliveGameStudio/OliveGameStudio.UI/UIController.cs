namespace OliveGameStudio;

/// <summary>
/// Manages the user interface (UI) elements and their interactions.
/// It provides functionality to add elements, manage focus on specific elements,
/// and handle events like pressing a button or other interactions.
/// </summary>
public sealed class UIController : IUIController
{
    readonly List<Element> _elements = [];
    readonly List<Node> _nodes = [];
    
    Button? _focusedElement;

    /// <summary>
    /// Invokes the specified action when the given button is pressed.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that should trigger the action when pressed.</param>
    /// <param name="action">The action to be executed in response to the button press event.</param>
    /// <remarks>
    /// This method associates a button press with a specific action, allowing custom behavior to be defined
    /// for each button press interaction within the user interface.
    /// </remarks>
    public void OnPressed(Button button, Action action)
    {
        Node node = Require(button);
        node.PressedAction = action;
    }

    /// <summary>
    /// Simulates a button press event, triggering the associated action of the currently focused button.
    /// </summary>
    /// <param name="button">An optional <see cref="Button"/> instance to explicitly focus before triggering the press. If null, the press acts on the current focused button.</param>
    /// <remarks>
    /// This method sets the focus to the specified button if provided and triggers its press action.
    /// If no button is provided and there is no currently focused button, no action is executed.
    /// </remarks>
    public void Press(Button? button = null)
    {
        if (button is not null)
        {
            Node target = Require(button);
            if (!target.Enabled)
            {
                // click on a disabled button → nothing
                return; 
            }
            
            FocusOn(button);
        }

        if (_focusedElement is not null)
        {
            Node node = Require(_focusedElement);
            if (!node.Enabled)
            {
                // never fire a disabled focus
                return; 
            }
            
            node.PressedAction?.Invoke();
        }
    }

    /// <summary>
    /// Associates a directional link between two buttons within the UI navigation graph.
    /// This method allows configuring navigational behavior in the specified direction.
    /// </summary>
    /// <param name="button">The source button that will be linked to another button.</param>
    /// <param name="direction">The direction in which the link will be established (e.g., Up, Down, Left, Right).</param>
    /// <param name="destination">The destination button that will be associated in the specified direction.</param>
    /// <exception cref="InvalidOperationException">Thrown when the source or destination button cannot be found in the UI hierarchy.</exception>
    /// <exception cref="NotSupportedException">Thrown if an invalid or unsupported direction is provided.</exception>
    public void Link(Button button, Direction direction, Button destination)
    {
        Node? existingNode = _nodes.FirstOrDefault(it => it.Button == button);
        if (existingNode is null)
        {
            throw new InvalidOperationException("Button not found.");
        }

        Node? destinationNode = _nodes.FirstOrDefault(it => it.Button == destination);
        if (destinationNode is null)
        {
            throw new InvalidOperationException("Destination button not found.");
        }

        switch (direction)
        {
            case Direction.Down: existingNode.Down = destinationNode; break;
            case Direction.Left: existingNode.Left = destinationNode; break;
            case Direction.Up: existingNode.Up = destinationNode; break;
            case Direction.Right: existingNode.Right = destinationNode; break;
            default: throw new NotSupportedException("Invalid direction.");
        }
    }

    /// <summary>
    /// Disables the specified button, preventing it from receiving input or triggering actions.
    /// </summary>
    /// <param name="button">The button to be disabled.</param>
    public void Disable(Button button) => SetEnabled(button, false);

    /// <summary>
    /// Enables the specified button, allowing it to be interacted with within the user interface.
    /// </summary>
    /// <param name="button">The button to enable within the UI system.</param>
    public void Enable(Button button) => SetEnabled(button, true);

    /// <summary>
    /// Retrieves the <see cref="Node"/> associated with the specified <see cref="Button"/>.
    /// </summary>
    /// <param name="button">The button for which the corresponding node is required.</param>
    /// <returns>The node associated with the specified button.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified button is not managed by the controller.</exception>
    Node Require(Button button) =>
        _nodes.FirstOrDefault(n => n.Button == button)
        ?? throw new InvalidOperationException($"Button '{button.Name}' is not managed by this controller.");
    
    /// <summary>
    /// Sets the enabled state of a specified button, determining whether it can receive input or trigger actions.
    /// </summary>
    /// <param name="button">The button whose enabled state is to be modified.</param>
    /// <param name="enabled">A boolean value indicating whether the button should be enabled (true) or disabled (false).</param>
    void SetEnabled(Button button, bool enabled)
    {
        Node node = Require(button);
        node.Enabled = enabled;
    
        if (!enabled && _focusedElement == button)
        {
            // disabling the focused button → re-home to the first enabled, or clear
            UnFocus();
            Node? next = _nodes.FirstOrDefault(n => n.Enabled);
            if (next is not null)
            {
                FocusOn(next.Button);
            }
        }
        else if (enabled && _focusedElement is null)
        {
            // enabling while nothing is focused → adopt
            FocusOn(button);
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

        if (element is not Button button)
        {
            return;
        }
        
        _nodes.Add(new Node(button));

        if (_focusedElement is null)
        {
            _focusedElement = button;
        }
    }

    /// <summary>
    /// Sets focus on the specified button, making it the currently active UI element
    /// within the controller.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance to be focused.</param>
    public void FocusOn(Button button)
    {
        _focusedElement = button;
    }

    /// <summary>
    /// Clears the focus from the currently focused user interface element, if any.
    /// After this method is called, no element will be focused until a new focus is explicitly set.
    /// </summary>
    /// <remarks>
    /// This method is typically used when the application requires no active element to be selected
    /// or when resetting the state of the UI.
    /// </remarks>
    public void UnFocus()
    {
        _focusedElement = null;
    }
}
