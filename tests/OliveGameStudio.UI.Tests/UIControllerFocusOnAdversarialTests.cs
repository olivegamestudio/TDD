namespace OliveGameStudio.Tests;

/// <summary>
/// QA's adversarial pass over the guard #101 added to <see cref="UIController.FocusOn"/>.
/// </summary>
/// <remarks>
/// The guard is one line, but it sits on the path three other members take to move focus —
/// <see cref="UIController.Press"/> focuses the button it presses, and <c>SetEnabled</c> both
/// re-homes focus away from a button it disables and adopts one it enables. A membership check
/// that refused any of those would break working behaviour rather than tighten it, so the cases
/// below press on the internal callers as hard as on the public one.
/// <para>
/// The other half is the claim the change lets <see cref="UIController.Press"/> make: that focus
/// can no longer be resting on a stranger by the time it is used. That is only true if
/// <em>every</em> route to <c>_focusedElement</c> checks, so these walk the routes.
/// </para>
/// </remarks>
public sealed class UIControllerFocusOnAdversarialTests
{
    // ---- the guard must not break the callers that move focus internally ----

    [Fact]
    public void DisablingTheFocusedButton_StillReHomesFocus()
    {
        // SetEnabled re-homes through FocusOn. The button it re-homes to comes out of _nodes,
        // so it is managed by construction — but if the guard had been written against
        // something other than membership, this is the first thing it would break.
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        controller.Disable(start);

        Assert.Same(options, controller.Focused);
    }

    [Fact]
    public void EnablingAButtonWhileNothingIsFocused_StillAdoptsIt()
    {
        // The other internal FocusOn. MenuScreen depends on this: the start button is disabled
        // until the save has been read, and enabling it is what focuses it.
        UIController controller = new();
        Button only = new("start");
        controller.Add(only);
        controller.Disable(only);

        Assert.Null(controller.Focused);

        controller.Enable(only);

        Assert.Same(only, controller.Focused);
    }

    [Fact]
    public void PressingAManagedButton_StillFocusesIt()
    {
        // Press(button) calls FocusOn on the way through. It Requires the button first, so the
        // guard is a second lookup rather than a new refusal — but only for a managed button.
        UIController controller = new();
        Button first = new("first");
        Button second = new("second");
        controller.Add(first);
        controller.Add(second);

        bool fired = false;
        controller.OnPressed(second, () => fired = true);

        controller.Press(second);

        Assert.Same(second, controller.Focused);
        Assert.True(fired);
    }

    // ---- the refusal itself ----

    [Fact]
    public void FocusOn_AButtonHeldByADifferentController_IsRefused()
    {
        // The case the rule exists for. The controller is a singleton in the shipping game, but
        // nothing stops a second one existing, and a button belonging to another controller is
        // as much a stranger as one belonging to nobody.
        UIController mine = new();
        UIController theirs = new();
        Button managed = new("BACK");
        Button elsewhere = new("BACK");
        mine.Add(managed);
        theirs.Add(elsewhere);

        Assert.Throws<InvalidOperationException>(() => mine.FocusOn(elsewhere));

        Assert.Same(managed, mine.Focused);
    }

    [Fact]
    public void FocusOn_AButtonAddedAfterTheRefusal_IsThenAccepted()
    {
        // Membership is read at the moment of the call, not remembered. A button refused before
        // it was added must be focusable once it is, or wiring order becomes load bearing.
        UIController controller = new();
        Button first = new("first");
        Button later = new("later");
        controller.Add(first);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(later));

        controller.Add(later);
        controller.FocusOn(later);

        Assert.Same(later, controller.Focused);
    }

    [Fact]
    public void FocusOn_AStrangerWhileNothingIsFocused_LeavesFocusEmpty()
    {
        // The refusal is inert against an empty focus as well as an occupied one: it must not
        // leave focus half-set, and the press that follows is a no-op rather than a throw.
        UIController controller = new();
        Button only = new("only");
        controller.Add(only);
        controller.Disable(only);

        Assert.Null(controller.Focused);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("stranger")));

        Assert.Null(controller.Focused);
        controller.Press();
    }

    [Fact]
    public void FocusOn_OnAControllerHoldingNothing_IsRefused()
    {
        // The boundary case: no nodes at all, so every button is a stranger. Before #101 this
        // set focus to a button that could never be resolved.
        UIController controller = new();

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("anything")));

        Assert.Null(controller.Focused);
    }

    [Fact]
    public void FocusOn_TheAlreadyFocusedButton_IsAcceptedAndChangesNothing()
    {
        // Re-aiming focus where it already is has to stay a no-op rather than becoming a
        // refusal, since Press does exactly this on every press of the focused button.
        UIController controller = new();
        Button only = new("only");
        controller.Add(only);

        controller.FocusOn(only);
        controller.FocusOn(only);

        Assert.Same(only, controller.Focused);
    }

    [Fact]
    public void FocusOn_ARefusedButton_DoesNotDisturbAPressInFlight()
    {
        // Held follows the button that was pressed, not focus. A refused aim taken between the
        // press and the release must not cost the player the press they are making.
        UIController controller = new();
        Button held = new("held");
        controller.Add(held);

        bool released = false;
        controller.OnReleased(held, () => released = true);

        controller.Press(held);
        Assert.Same(held, controller.Held);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("stranger")));

        Assert.Same(held, controller.Held);

        controller.Release();

        Assert.True(released);
    }

    // ---- the claim the guard lets Press make ----

    [Fact]
    public void NoSequenceOfFocusMoves_LeavesFocusOnAStranger()
    {
        // Press's doc comment now says the focused button can no longer be a stranger, because
        // every route to focus checks membership. That is a claim about Add, FocusOn, Disable's
        // re-home, Enable's adopt and UnFocus together — so walk them all and then press.
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        Button quit = new("quit");
        controller.Add(start);
        controller.Add(options);
        controller.Add(quit);

        int fired = 0;
        controller.OnPressed(start, () => fired++);
        controller.OnPressed(options, () => fired++);
        controller.OnPressed(quit, () => fired++);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("start")));
        controller.Disable(start);
        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("options")));
        controller.Disable(options);
        controller.Enable(start);
        controller.UnFocus();
        controller.Enable(options);
        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("quit")));

        // Whatever focus landed on, it is something this controller holds — so this resolves
        // rather than throwing, which is the whole of what #101 buys the press path.
        controller.Press();

        Assert.Equal(1, fired);
    }

    [Fact]
    public void AStrangerCannotDisplaceFocusEvenWhenItSharesTheFocusedButtonsName()
    {
        // The identity rule and the membership rule meeting: the stranger is refused, and the
        // focus that stays is the same object rather than an equal one.
        UIController controller = new();
        Button managed = new("BACK");
        controller.Add(managed);
        controller.FocusOn(managed);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("BACK")));

        Assert.Same(managed, controller.Focused);
    }

    // ---- state is not membership ----

    [Fact]
    public void FocusingADisabledButton_LeavesItFocusedAndUnpressable()
    {
        // #101 deliberately checks membership and not state. The consequence has to be
        // survivable: a disabled button may hold focus, and pressing it does nothing at all
        // rather than throwing or firing.
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);
        controller.Disable(options);

        bool fired = false;
        controller.OnPressed(options, () => fired = true);

        controller.FocusOn(options);

        Assert.Same(options, controller.Focused);

        controller.Press();

        Assert.False(fired);
        Assert.Null(controller.Held);

        // ...and it becomes pressable again the moment it is enabled, with focus never moving.
        controller.Enable(options);
        controller.Press();

        Assert.True(fired);
        Assert.Same(options, controller.Focused);
    }
}
