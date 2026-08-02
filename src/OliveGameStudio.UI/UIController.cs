namespace OliveGameStudio;

/// <summary>
/// Represents the controller responsible for managing interactions and states
/// of user interface (UI) elements in the application.
/// </summary>
/// <remarks>
/// Handles addition of elements, focus management, button actions,
/// enabling or disabling elements, and directional navigation between buttons.
/// </remarks>
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
    /// Associates a specified action with the release event of the given button.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that triggers the action when released.</param>
    /// <param name="action">The action to be executed in response to the button release event.</param>
    /// <remarks>
    /// This method allows custom behavior to be defined for the release interaction of a specific button
    /// within the user interface.
    /// </remarks>
    public void OnReleased(Button button, Action action)
    {
        Node node = Require(button);
        node.ReleasedAction = action;
    }

    Button? _pressing;

    /// <summary>
    /// Gets the currently focused button in the user interface.
    /// </summary>
    /// <remarks>
    /// The <c>Focused</c> property returns the button that is currently
    /// receiving user interactions or is marked as active in the UI.
    /// If no button is focused, the property returns <c>null</c>.
    /// </remarks>
    public Button? Focused => _focusedElement;

    /// <summary>
    /// Gets the button currently being held down in the user interface.
    /// </summary>
    /// <remarks>
    /// The <c>Held</c> property represents the button that is actively
    /// engaged between a press and release action. This property will
    /// return <c>null</c> if no button is being held at the moment.
    /// <para>
    /// It follows the button that was pressed, which is not necessarily the focused one: a pressed
    /// action may move focus, and a held button may be disabled, without either changing what the
    /// player is holding down.
    /// </para>
    /// </remarks>
    public Button? Held => _pressing;

    /// <summary>
    /// Determines whether the specified button is currently enabled.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance to check for the enabled state.</param>
    /// <returns>
    /// A boolean value indicating whether the button is enabled.
    /// Returns <c>true</c> if the button is enabled; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method retrieves the enabled state of a button managed by the UI controller.
    /// Ensure that the button is properly registered with the controller before invoking this method.
    /// </remarks>
    public bool IsEnabled(Button button) => Require(button).Enabled;

    /// <summary>
    /// Triggers the pressing action for the specified button or the currently focused element.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> to be pressed. If null, the action applies to the currently focused element.</param>
    /// <remarks>
    /// If a specific button is provided and it is enabled, the focus will move to this button, and its associated action will be processed.
    /// In the absence of a specified button, the currently focused element is pressed, provided it is enabled.
    /// <para>
    /// The press is held by the button that was pressed, not by whatever is focused once the pressed
    /// action returns. The action is free to move focus — <see cref="FocusOn"/> directly, or
    /// <see cref="Disable"/>, which re-homes focus as a side effect — and the release still commits
    /// the button the player pressed.
    /// </para>
    /// </remarks>
    public void Press(Button? button = null)
    {
        if (button is not null)
        {
            Node target = Require(button);
            if (!target.Enabled)
            {
                return;
            }

            FocusOn(button);
        }

        if (_focusedElement is null)
        {
            return;
        }

        // Captured before the action runs: _focusedElement may point somewhere else by the
        // time it returns, and the press belongs to the button the player actually pressed.
        Button pressed = _focusedElement;
        Node node = Require(pressed);
        if (!node.Enabled)
        {
            return;
        }

        // Armed before the action runs so that a pressed action may Cancel() the press it
        // started; arming afterwards would overwrite that decision.
        _pressing = pressed;
        node.PressedAction?.Invoke();
    }

    /// <summary>
    /// Releases the currently held button and triggers its associated action if enabled.
    /// </summary>
    /// <remarks>
    /// If a button is currently being pressed, this method sets it to null, retrieves its associated node,
    /// and invokes the node's press action if the node is enabled. No action is performed if no button
    /// is currently pressed.
    /// <para>
    /// Enablement is read here, at the moment of release, not at the moment of the press. Disabling a
    /// held button therefore suppresses its commit, and re-enabling it before the player lets go
    /// restores it. <see cref="Disable"/> governs whether a button may act; <see cref="Cancel"/> is the
    /// way to abandon a press outright.
    /// </para>
    /// </remarks>
    public void Release()
    {
        if (_pressing is null)
        {
            return;
        }
        
        Node node = Require(_pressing);
        _pressing = null;
        
        if (node.Enabled)
        {
            node.ReleasedAction?.Invoke();
        }
    }

    /// <summary>
    /// Cancels any currently active button press operation without triggering the associated action.
    /// </summary>
    /// <remarks>
    /// This method resets the state of the pressing operation in the UI controller, ensuring that
    /// the action linked to the currently held button is not executed. Typically used to handle scenarios
    /// where an input operation is interrupted or deliberately aborted.
    /// <para>
    /// A pressed action may cancel the very press that invoked it: <see cref="Press"/> arms the hold
    /// before running the action, so the decision made inside it stands and the button does not commit
    /// on release. This is the way to abandon a press — <see cref="Disable"/> only withholds the
    /// commit, and the press it belongs to survives being re-enabled.
    /// </para>
    /// </remarks>
    public void Cancel() => _pressing = null;

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
    /// <remarks>
    /// Disabling the focused button moves focus: it is re-homed to the first enabled button, or
    /// cleared if there is none. That side effect is worth knowing about because disabling a button
    /// as it activates — so it cannot fire twice — is an idiom this codebase uses. It does not
    /// disturb a press in flight: <see cref="Held"/> follows the button that was pressed, not focus,
    /// so the press stays where it started and is merely withheld from committing while the button
    /// is disabled.
    /// </remarks>
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
