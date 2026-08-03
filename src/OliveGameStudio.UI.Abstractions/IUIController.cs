namespace OliveGameStudio;

/// <summary>
/// Defines the contract for a UI controller that manages and interacts with UI elements, such as buttons.
/// </summary>
/// <remarks>
/// Buttons are resolved by identity, never by name. An implementation is expected to be shared
/// between screens, so two screens may label a button the same thing and must still be able to
/// enable, wire and press their own.
/// <para>
/// It follows that a button this controller does not hold is a stranger even when its name matches
/// one that is held exactly, and every method here that takes a <see cref="Button"/> reports it by
/// throwing rather than by acting on the namesake. Adding a button is what makes it known;
/// <see cref="Add"/> is the only way in.
/// </para>
/// </remarks>
public interface IUIController
{
    /// <summary>
    /// Associates an action to be executed when the specified button is pressed.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that triggers the associated action when pressed.</param>
    /// <param name="action">The action to execute in response to the button press event.</param>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name, since the wiring belongs to the button rather than to the label it wears.
    /// </exception>
    void OnPressed(Button button, Action action);

    /// <summary>
    /// Associates an action to be executed when the specified button is released.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance that triggers the associated action when released.</param>
    /// <param name="action">The action to execute in response to the button release event.</param>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name. Wiring a stranger throws rather than overwriting the namesake's handler.
    /// </exception>
    void OnReleased(Button button, Action action);

    /// <summary>
    /// Simulates a button press event, optionally focusing on the specified button before triggering its associated action.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> to focus and press. If null, the press is executed on the currently focused button, if any.</param>
    /// <remarks>
    /// The press is held by the button that was pressed. A pressed action that moves focus — directly,
    /// or by disabling its own button — does not hand the press to whatever focus lands on.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name. When none is given the focused button is resolved the same way, though it can no
    /// longer be a stranger: every route to focus checks membership, and an element once added is
    /// never removed.
    /// </exception>
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
    /// <exception cref="InvalidOperationException">
    /// Either end is not managed by this controller — including when a managed button shares its
    /// name. Navigation is wired between the buttons themselves, so a link can never be pointed at
    /// a namesake by accident.
    /// </exception>
    void Link(Button button, Direction direction, Button destination);

    /// <summary>
    /// Disables the specified button, preventing it from receiving input or triggering actions.
    /// </summary>
    /// <param name="button">The button to be disabled.</param>
    /// <remarks>
    /// Disabling the focused button re-homes focus to the first enabled button, or clears it if
    /// there is none. A press already in flight is unaffected — it stays with the button it started
    /// on and is only withheld from committing while that button is disabled.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name, so one screen cannot grey out another screen's <c>BACK</c> by naming its own the same.
    /// </exception>
    void Disable(Button button);

    /// <summary>
    /// Enables the specified button, allowing it to interact and function within the application.
    /// </summary>
    /// <param name="button">The button to be enabled.</param>
    /// <remarks>
    /// The counterpart to the re-homing in <see cref="Disable"/>: enabling a button while nothing
    /// is focused adopts it, so a screen that disabled its way down to no focus at all has
    /// somewhere for input to go again as soon as one button comes back.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name.
    /// </exception>
    void Enable(Button button);

    /// <summary>
    /// Adds a new UI element to the internal collection managed by the controller.
    /// This allows the element to participate in UI interactions such as receiving focus or being pressed.
    /// </summary>
    /// <param name="element">The UI element to be added to the controller.</param>
    /// <remarks>
    /// The guard below is on the button, never on its name: a second button labelled the same as
    /// one already added is a different button and is accepted, which is the whole point of the
    /// controller being shared between screens.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is already managed by this controller. Adding it twice would leave a second,
    /// unreachable registration behind, so it fails rather than half working.
    /// </exception>
    void Add(Element element);

    /// <summary>
    /// Sets focus on the specified button, making it the currently active UI element
    /// within the controller.
    /// </summary>
    /// <param name="button">The <see cref="Button"/> instance to be focused.</param>
    /// <remarks>
    /// The button must be one this controller holds, so focus can never be left pointing at a
    /// stranger. Only membership is checked: a managed button that is currently disabled may still
    /// be focused, because disabled is a statement about pressing — which <see cref="Press"/>
    /// declines on its own — and refusing the focus as well would be a wider rule than this one.
    /// Implementations that refuse leave the existing focus where it was.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The button is not managed by this controller — including when a managed button shares its
    /// name, since a same-named button belonging to another screen is a different button. The
    /// refusal is raised where the aim is taken rather than by whatever later reads the focus.
    /// </exception>
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

    /// <summary>
    /// Gets whether any element currently holds focus.
    /// </summary>
    /// <value>
    /// <c>true</c> while a button is focused; otherwise <c>false</c>.
    /// </value>
    /// <remarks>
    /// This is the question input routing asks each frame: a key press belongs to the UI while
    /// something here is focused, and falls through to the ship when nothing is. Focus arrives and
    /// leaves by more routes than a caller drives — <see cref="Add"/> adopts the first button,
    /// <see cref="Disable"/> re-homes or clears, <see cref="Enable"/> adopts again — so the router
    /// has to ask rather than track what it last set.
    /// <para>
    /// It reports the presence of a focus, not which button holds it, because that is the whole of
    /// what the routing decision needs and every other member here is deliberately blind to which
    /// button a controller it does not own is on. Handing back the focused <see cref="Button"/>
    /// would let a consumer act on a button belonging to another screen, which is the thing this
    /// interface spends its exceptions preventing.
    /// </para>
    /// <para>
    /// Focus is a claim about a button, not about whether it can be pressed: a focused button that
    /// is disabled still reports <c>true</c>. Disabled is a statement about pressing, which
    /// <see cref="Press"/> declines on its own, and a menu whose only button is greyed out is still
    /// a menu the player is looking at.
    /// </para>
    /// </remarks>
    bool HasFocus { get; }
}