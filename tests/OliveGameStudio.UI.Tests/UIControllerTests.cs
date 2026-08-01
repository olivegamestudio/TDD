namespace OliveGameStudio.Tests;

public sealed class UIControllerTests
{
    // ---- focus / add -------------------------------------------------------
    // Focus is asserted DIRECTLY via Focused — no action wiring needed.
    // (Previously these observed focus indirectly through press-actions.)

    [Fact]
    public void FirstButtonAdded_BecomesFocused()
    {
        UIController controller = new();
        Button start = new("start");

        controller.Add(start);

        Assert.Same(start, controller.Focused);
    }

    [Fact]
    public void LaterButtonsAdded_DoNotStealFocus()
    {
        UIController controller = new();
        Button first = new("first");
        controller.Add(first);

        controller.Add(new Button("second"));

        Assert.Same(first, controller.Focused);
    }

    [Fact]
    public void FocusOn_OverridesTheDefault()
    {
        UIController controller = new();
        Button first = new("first");
        Button chosen = new("chosen");
        controller.Add(first);
        controller.Add(chosen);

        controller.FocusOn(chosen);

        Assert.Same(chosen, controller.Focused);
    }

    [Fact]
    public void UnFocus_ClearsFocused()
    {
        UIController controller = new();
        controller.Add(new Button("start"));

        controller.UnFocus();

        Assert.Null(controller.Focused);
    }

    [Fact]
    public void DisabledFirstButton_LeavesNothingFocused()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        controller.Disable(start);              // focused -> re-home; none enabled -> clears

        Assert.Null(controller.Focused);
    }

    [Fact]
    public void EnablingWhileNothingFocused_AdoptsFocus()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);              // waiting on a service...

        controller.Enable(start);               // service ready

        Assert.Same(start, controller.Focused);
    }

    [Fact]
    public void IsEnabled_TracksDisableAndEnable()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        Assert.True(controller.IsEnabled(start));
        controller.Disable(start);
        Assert.False(controller.IsEnabled(start));
        controller.Enable(start);
        Assert.True(controller.IsEnabled(start));
    }

    // ---- the two hooks: pressed fires on DOWN, released fires on UP --------
    // This pinning matters: a test that wires OnPressed and then calls
    // Press(); Release(); stays green even if Release() is broken — the down
    // fired it. Commit tests MUST observe through OnReleased.

    [Fact]
    public void PressDown_FiresThePressedHook_NotTheReleasedHook()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool down = false, up = false;
        controller.OnPressed(start, () => down = true);
        controller.OnReleased(start, () => up = true);

        controller.Press();                     // down only

        Assert.True(down);
        Assert.False(up);                       // the commit has NOT happened yet
    }

    [Fact]
    public void Release_Commits()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);   // observed on the RELEASE hook

        controller.Press();
        controller.Release();

        Assert.True(fired);
    }

    [Fact]
    public void PressDown_Alone_DoesNotCommit()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press();                     // down, never released

        Assert.False(fired);                    // the launch is on release
    }

    [Fact]
    public void Release_Commits_TheFocused_NotItsSiblings()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);                  // auto-focused
        controller.Add(options);                // sibling — unfocused

        Button? released = null;
        controller.OnReleased(start, () => released = start);
        controller.OnReleased(options, () => released = options);

        controller.Press();
        controller.Release();

        Assert.Same(start, released);
    }

    [Fact]
    public void Cancel_AbortsWithoutCommitting()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press();
        controller.Cancel();                    // mouse left the button / press aborted

        Assert.False(fired);
        Assert.Null(controller.Held);
    }

    [Fact]
    public void Held_IsArmed_BetweenPressAndRelease()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        controller.Press();
        Assert.Same(start, controller.Held);    // armed while held down

        controller.Release();
        Assert.Null(controller.Held);           // cleared on release
    }

    [Fact]
    public void PressRelease_OnEmptyController_FiresNothing()
    {
        UIController controller = new();

        controller.Press();
        controller.Release();                   // no focus — must not throw or fire
    }

    [Fact]
    public void DisabledButton_NeverCommits()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press();
        controller.Release();

        Assert.False(fired);
    }

    // ---- press actions that change the controller's own state ---------------
    // The press action runs while the press is still open, so it can move focus
    // or disable a button out from under the hold. The hold belongs to the button
    // the player pressed, and nothing the action does may transfer it elsewhere.

    [Fact]
    public void Held_TracksThePressedButton_EvenIfThePressActionMovesFocus()
    {
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        controller.OnPressed(a, () => controller.FocusOn(b));

        controller.Press(a);

        Assert.Same(a, controller.Held);        // the press is a's, wherever focus went
        Assert.Same(b, controller.Focused);     // the action's focus move still stands
    }

    [Fact]
    public void PressAction_ThatMovesFocus_StillCommitsThePressedButton()
    {
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        Button? released = null;
        controller.OnPressed(a, () => controller.FocusOn(b));
        controller.OnReleased(a, () => released = a);
        controller.OnReleased(b, () => released = b);

        controller.Press(a);
        controller.Release();

        Assert.Same(a, released);
    }

    [Fact]
    public void PressAction_ThatDisablesItsOwnButton_MustNotCommitADifferentButton()
    {
        // MenuScreen.OnStartReleased already uses this idiom: disable the button as it
        // activates so it cannot be triggered twice. Doing that on the DOWN hook must not
        // hand the press over to whichever button focus re-homes to.
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);                  // auto-focused
        controller.Add(options);

        List<string> committed = [];
        controller.OnPressed(start, () => controller.Disable(start));
        controller.OnReleased(start, () => committed.Add("start"));
        controller.OnReleased(options, () => committed.Add("options"));

        controller.Press(start);
        controller.Release();

        Assert.Empty(committed);
    }

    [Fact]
    public void PressAction_ThatCancels_AbortsTheHold()
    {
        // The press is armed before the action runs, so an action that aborts the
        // press mid-flight stays aborted rather than being re-armed underneath it.
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnPressed(start, () => controller.Cancel());
        controller.OnReleased(start, () => fired = true);

        controller.Press();

        Assert.Null(controller.Held);

        controller.Release();

        Assert.False(fired);
    }

    // ---- disabling a button that is currently held --------------------------
    // Settled here rather than left to Release()'s enabled check: a button that
    // cannot be interacted with does not own a press.

    [Fact]
    public void DisablingTheHeldButton_CancelsTheHold()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press(start);
        controller.Disable(start);              // withdrawn mid-press

        Assert.Null(controller.Held);

        controller.Release();

        Assert.False(fired);
    }

    [Fact]
    public void ReEnablingAfterDisablingTheHeldButton_DoesNotResurrectTheCommit()
    {
        // The hold is gone for good — a button that flickers back on mid-press does
        // not inherit a press the player can no longer see they are making.
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press(start);
        controller.Disable(start);
        controller.Enable(start);

        Assert.Null(controller.Held);

        controller.Release();

        Assert.False(fired);
    }

    [Fact]
    public void DisablingAnUnheldButton_LeavesTheHoldAlone()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press(start);
        controller.Disable(options);            // a sibling goes away — not our press

        Assert.Same(start, controller.Held);

        controller.Release();

        Assert.True(fired);
    }

    // ---- mouse / touch through Press(button) -------------------------------

    [Fact]
    public void MouseDownUp_OnButton_Commits()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);                  // start auto-focused
        controller.Add(options);

        Button? released = null;
        controller.OnReleased(start, () => released = start);
        controller.OnReleased(options, () => released = options);

        controller.Press(options);              // down over the unfocused one
        controller.Release();                   // up over it

        Assert.Same(options, released);         // focused + committed options, not start
    }

    [Fact]
    public void MouseDown_ThenLeavesAndReleasesOff_Cancels()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        bool fired = false;
        controller.OnReleased(options, () => fired = true);

        controller.Press(options);              // down over options (armed)
        controller.Cancel();                    // mapper hit-tests up-off-target

        Assert.False(fired);
    }

    [Fact]
    public void MouseDown_OnDisabled_DoesNothing()
    {
        UIController controller = new();
        Button start = new("start");
        Button locked = new("locked");
        controller.Add(start);
        controller.Add(locked);
        controller.Disable(locked);

        bool fired = false;
        controller.OnReleased(locked, () => fired = true);

        controller.Press(locked);
        controller.Release();

        Assert.False(fired);
    }

    // ---- graph wiring -------------------------------------------------------

    [Fact]
    public void Link_UnknownSourceButton_Throws()
    {
        UIController controller = new();
        Button known = new("known");
        controller.Add(known);

        Assert.Throws<InvalidOperationException>(
            () => controller.Link(new Button("ghost"), Direction.Down, known));
    }

    [Fact]
    public void Link_UnknownDestinationButton_Throws()
    {
        UIController controller = new();
        Button known = new("known");
        controller.Add(known);

        Assert.Throws<InvalidOperationException>(
            () => controller.Link(known, Direction.Down, new Button("ghost")));
    }
}
