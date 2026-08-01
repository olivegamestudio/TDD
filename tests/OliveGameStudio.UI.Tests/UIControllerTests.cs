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

    // ---- the press belongs to the button that was pressed -------------------
    // A pressed action may move focus — deliberately via FocusOn, or as a side
    // effect of disabling its own button, which re-homes focus. The press must
    // stay with the button the player actually pressed, so the release commits
    // that button and no other.

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

        Assert.Empty(committed);                // options was never touched by the player
    }

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

        Assert.Same(a, controller.Held);
    }

    [Fact]
    public void Release_Commits_TheButtonThatWasPressed_NotWhereFocusMoved()
    {
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        List<string> committed = [];
        controller.OnPressed(a, () => controller.FocusOn(b));   // press action re-aims focus
        controller.OnReleased(a, () => committed.Add("a"));
        controller.OnReleased(b, () => committed.Add("b"));

        controller.Press(a);
        controller.Release();

        Assert.Equal("a", Assert.Single(committed));    // a is still enabled, so a commits
    }

    [Fact]
    public void PressAction_MayCancelThePressItStarted()
    {
        // The press is armed before the pressed hook runs, so a hook that decides the
        // press should not stand — a drag threshold, a guard that rejects the input —
        // can abort it with Cancel() and have that stick.
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnPressed(start, controller.Cancel);
        controller.OnReleased(start, () => fired = true);

        controller.Press();

        Assert.Null(controller.Held);           // the cancel was not overwritten

        controller.Release();

        Assert.False(fired);
    }

    // ---- what a disabled button does to a press in flight -------------------
    // Settled here: disabling a held button suppresses its commit but does not
    // cancel the press. Enablement is read at the moment of release, so a button
    // re-enabled before the player lets go still commits. Disable governs whether
    // a button may act; Cancel is the way to abandon a press outright.

    [Fact]
    public void DisablingTheHeldButton_SuppressesItsCommit()
    {
        UIController controller = new();
        Button start = new("start");
        Button other = new("other");
        controller.Add(start);
        controller.Add(other);                  // keeps focus re-homing in play

        bool fired = false;
        controller.OnPressed(start, () => controller.Disable(start));
        controller.OnReleased(start, () => fired = true);

        controller.Press(start);
        controller.Release();

        Assert.False(fired);                    // disabled at release → no commit
    }

    [Fact]
    public void HeldButton_ReEnabledBeforeRelease_StillCommits()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);

        controller.Press();
        controller.Disable(start);              // e.g. a service went away mid-press...
        controller.Enable(start);               // ...and came back before the player let go

        controller.Release();

        Assert.True(fired);                     // enablement is read at release
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
