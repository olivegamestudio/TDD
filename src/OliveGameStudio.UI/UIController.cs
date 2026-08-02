namespace OliveGameStudio;

/// <summary>
/// Represents the controller responsible for managing interactions and states
/// of user interface (UI) elements in the application.
/// </summary>
/// <remarks>
/// Handles addition of elements, focus management, button actions,
/// enabling or disabling elements, and directional navigation between buttons.
/// <para>
/// Every lookup resolves a <see cref="Button"/> by identity, never by name. The controller is
/// registered as a singleton, so all of a game's screens share one node list, and two screens are
/// free to label a button the same obvious thing without either author knowing the other did.
/// <see cref="Element"/> is what makes that safe; nothing here should compare names.
/// </para>
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
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. A button that merely shares a name with a
    /// managed one is not managed, so the wiring cannot land on the wrong button.
    /// </exception>
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
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. A same-named stranger throws here rather than
    /// overwriting the managed button's handler, which is what used to happen.
    /// </exception>
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
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. There is no state to report for a button it
    /// does not hold, and a managed button of the same name is a different button whose state
    /// would be a wrong answer rather than a near one.
    /// </exception>
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
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. When none is given the focused button is
    /// resolved the same way, though it can no longer be a stranger: every route to focus checks
    /// membership since <see cref="FocusOn"/> began to, and an element once added is never removed.
    /// The check on that path is what keeps that true rather than a report of it failing.
    /// </exception>
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
    /// <remarks>
    /// Both ends are matched by identity. A button sharing a name with a managed one cannot be
    /// found, so a screen cannot accidentally wire its navigation into another screen's menu.
    /// </remarks>
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
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. Since every screen shares one controller, a
    /// same-named button belonging to another screen is a stranger here and is refused rather than
    /// greyed out.
    /// </exception>
    public void Disable(Button button) => SetEnabled(button, false);

    /// <summary>
    /// Enables the specified button, allowing it to be interacted with within the user interface.
    /// </summary>
    /// <param name="button">The button to enable within the UI system.</param>
    /// <remarks>
    /// The counterpart to the re-homing in <see cref="Disable"/>: enabling a button while nothing
    /// is focused adopts it. Disabling can leave a screen with no focus at all — it clears focus
    /// when no button is left enabled to take it — and this is what gives input somewhere to go
    /// again. <c>MenuScreen</c> relies on it: the start button is disabled until the save has been
    /// read, and enabling it is what focuses it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller, a same-named one belonging to another screen
    /// included.
    /// </exception>
    public void Enable(Button button) => SetEnabled(button, true);

    /// <summary>
    /// Retrieves the <see cref="Node"/> associated with the specified <see cref="Button"/>.
    /// </summary>
    /// <param name="button">The button for which the corresponding node is required.</param>
    /// <returns>The node associated with the specified button.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified button is not managed by the controller.</exception>
    /// <remarks>
    /// Matches the button itself, not its name. A button that merely shares a name with a managed
    /// one is a stranger, and is reported as one.
    /// </remarks>
    Node Require(Button button) =>
        _nodes.FirstOrDefault(n => n.Button == button)
        ?? throw new InvalidOperationException($"Button '{button.Name}' is not managed by this controller.");
    
    /// <summary>
    /// Sets the enabled state of a specified button, determining whether it can receive input or trigger actions.
    /// </summary>
    /// <param name="button">The button whose enabled state is to be modified.</param>
    /// <param name="enabled">A boolean value indicating whether the button should be enabled (true) or disabled (false).</param>
    /// <remarks>
    /// The focus comparison below is identity, like every other lookup here: disabling one screen's
    /// button must not unfocus another screen's button of the same name.
    /// </remarks>
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
    /// <remarks>
    /// The guard keys on the button, never on its name. A second button labelled the same as one
    /// already added is a different button and is accepted — refusing it would turn the very case
    /// this rule exists for, two screens each with a <c>BACK</c>, into a startup exception.
    /// Non-button elements are held but not made into nodes, so they are neither focusable nor
    /// subject to the guard.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is already managed by this controller. A second node for it would be
    /// unreachable by construction — every lookup finds the first — so the double add fails
    /// loudly rather than leaving a button that silently stops responding.
    /// </exception>
    public void Add(Element element)
    {
        if (element is Button existing && _nodes.Any(n => n.Button == existing))
        {
            throw new InvalidOperationException(
                $"Button '{existing.Name}' is already managed by this controller.");
        }

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
    /// <remarks>
    /// Membership is checked here and not only when the focus is used. This entry point used to be
    /// the exception, which meant focus could rest on a button this controller does not hold and
    /// the next <see cref="Press"/> threw on the caller's behalf, somewhere the mistake was no
    /// longer visible. Only membership is checked: a managed button that is currently disabled may
    /// still be focused. Disabled is a statement about pressing, which <see cref="Press"/> already
    /// declines on its own; refusing the focus as well would take a decision this type has never
    /// made — <see cref="Add"/> focuses the first button added without asking, and <see
    /// cref="Disable"/> only re-homes focus away from a button when there is an enabled one to
    /// take it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller. A same-named button belonging to another
    /// screen is a stranger here, as it is everywhere else in this type.
    /// </exception>
    public void FocusOn(Button button)
    {
        Require(button);
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
