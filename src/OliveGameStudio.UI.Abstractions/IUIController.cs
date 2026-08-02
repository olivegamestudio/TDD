namespace OliveGameStudio;

/// <summary>
/// Defines the contract for a UI controller that manages and interacts with UI elements, such as buttons.
/// </summary>
public interface IUIController
{
    /// <summary>
    /// Associates an action to be executed when the specified button is pressed.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that triggers the associated action when pressed.</param>
    /// <param name="action">The action to execute in response to the button press event.</param>
    void OnPressed(Button button, Action action);

    /// <summary>
    /// Associates an action to be executed when the specified button is released.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that triggers the associated action when released.</param>
    /// <param name="action">The action to execute in response to the button release event.</param>
    void OnReleased(Button button, Action action);

    /// <summary>
    /// Simulates a button press event, optionally focusing on the specified button before triggering its associated action.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> to focus and press. If null, the press is executed on the currently focused button, if any.</param>
    /// <remarks>
    /// The press is held by the button that was pressed. A pressed action that moves focus — directly,
    /// or by disabling its own button — does not hand the press to whatever focus lands on.
    /// </remarks>
    void Press(Button? button = null);

    /// <summary>
    /// Releases the button currently being pressed and executes its associated action if it is enabled.
    /// </summary>
    /// <remarks>
    /// This method stops the ongoing press action by resetting the pressed button to null and,
    /// if the associated node is enabled, triggers its corresponding action. If no button is currently being pressed, the method performs no operation.
    /// Enablement is read at release, so disabling a held button suppresses its commit and re-enabling
    /// it before release restores it.
    /// </remarks>
    void Release();

    /// <summary>
    /// Establishes a directional link between the specified button and a destination button.
    /// This link defines a navigation relationship, allowing the UI to traverse from one button to another in the given direction.
    /// </summary>
    /// <param name="button">The source button that serves as the starting point of the link.</param>
    /// <param name="direction">The direction in which the link is established. This specifies the navigation flow.</param>
    /// <param name="destination">The destination button that is linked to the source button in the specified direction.</param>
    void Link(Button button, Direction direction, Button destination);

    /// <summary>
    /// Disables the specified button, preventing it from receiving input or triggering actions.
    /// </summary>
    /// <param name="button">The button to be disabled.</param>
    void Disable(Button button);

    /// <summary>
    /// Enables the specified component, allowing it to interact and function within the application.
    /// </summary>
    /// <param name="component">The component instance to be enabled.</param>
    void Enable(Button button);

    /// <summary>
    /// Adds a new UI element to the internal collection managed by the controller.
    /// This allows the element to participate in UI interactions such as receiving focus or being pressed.
    /// </summary>
    /// <param name="element">The UI element to be added to the controller.</param>
    void Add(Element element);

    /// <summary>
    /// Sets focus on the specified button, making it the currently active UI element
    /// within the controller.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance to be focused.</param>
    void FocusOn(Button button);

    /// <summary>
    /// Clears the current focus from any UI element, if one is currently focused.
    /// After this method executes, there will be no active or focused UI element
    /// until focus is explicitly set again.
    /// </summary>
    /// <remarks>
    /// This method is useful when no element should remain actively focused,
    /// such as during UI state transitions or application resets.
    /// </remarks>
    void UnFocus();
}