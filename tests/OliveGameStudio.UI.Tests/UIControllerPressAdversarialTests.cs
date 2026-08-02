namespace OliveGameStudio.Tests;

/// <summary>
/// Boundary cases for the press-ownership fix in <see cref="UIController.Press"/> — added by QA
/// while trying to break it, and kept as regression coverage.
/// </summary>
/// <remarks>
/// The fix moved two things: the pressed button is captured before the pressed action runs, and
/// the press is armed before it runs. Both are observable only from a pressed action that reaches
/// back into the controller, so that is what these exercise — a hook that cancels, that releases,
/// that presses again, and that disables the button out from under itself. Every button here has a
/// distinct name, so nothing depends on how <see cref="Button"/> record equality matches nodes;
/// that is issue #13's territory and must not be masked here.
/// </remarks>
public sealed class UIControllerPressAdversarialTests
{
    // ---- focus that vanishes rather than moving -----------------------------

    [Fact]
    public void PressAction_ThatDisablesTheOnlyButton_StillHoldsIt()
    {
        // The sibling symptom of the reported defect. With another button present the press was
        // stolen; with no other button to re-home to, focus went null and the press was armed with
        // nothing — the press was dropped instead of misrouted. Same root cause, so it is pinned
        // from this side too.
        UIController controller = new();
        Button only = new("only");
        controller.Add(only);

        controller.OnPressed(only, () => controller.Disable(only));

        controller.Press(only);

        Assert.Null(controller.Focused);            // nothing left to focus
        Assert.Same(only, controller.Held);         // but the player is still holding it
    }

    [Fact]
    public void PressAction_ThatDisablesTheOnlyButton_CommitsNothingOnRelease()
    {
        UIController controller = new();
        Button only = new("only");
        controller.Add(only);

        bool fired = false;
        controller.OnPressed(only, () => controller.Disable(only));
        controller.OnReleased(only, () => fired = true);

        controller.Press(only);
        controller.Release();

        Assert.False(fired);                        // disabled at the moment of release
        Assert.Null(controller.Held);
    }

    // ---- the press hook reaching back into the controller -------------------

    [Fact]
    public void PressAction_ThatCancelsAndThenMovesFocus_StaysCancelled()
    {
        // Cancel() followed by a focus move is the nastiest ordering for the old code: the
        // trailing assignment would have re-armed the press *and* aimed it at the new focus.
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        List<string> committed = [];
        controller.OnPressed(a, () =>
        {
            controller.Cancel();
            controller.FocusOn(b);
        });
        controller.OnReleased(a, () => committed.Add("a"));
        controller.OnReleased(b, () => committed.Add("b"));

        controller.Press(a);

        Assert.Null(controller.Held);

        controller.Release();

        Assert.Empty(committed);
    }

    [Fact]
    public void PressAction_ThatReleasesTheDirectPressItStarted_CommitsExactlyOnce()
    {
        // A hook that completes the interaction itself. The commit must not then happen a second
        // time when the player actually lets go.
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        int commits = 0;
        controller.OnPressed(start, controller.Release);
        controller.OnReleased(start, () => commits++);

        controller.Press(start);

        Assert.Null(controller.Held);               // the inner release consumed the press

        controller.Release();                       // the player lets go afterwards

        Assert.Equal(1, commits);
    }

    [Fact]
    public void Release_CommitsThePressedButton_WithoutDisturbingWhereFocusWasMovedTo()
    {
        // The commit follows the press; focus is left where the pressed action put it. Release
        // must not "restore" focus to the button it committed.
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        List<string> committed = [];
        controller.OnPressed(a, () => controller.FocusOn(b));
        controller.OnReleased(a, () => committed.Add("a"));
        controller.OnReleased(b, () => committed.Add("b"));

        controller.Press(a);
        controller.Release();

        Assert.Equal("a", Assert.Single(committed));
        Assert.Same(b, controller.Focused);
    }

    // ---- presses that never arm --------------------------------------------

    [Fact]
    public void Press_OnADisabledButton_HoldsNothing_AndLeavesFocusAlone()
    {
        // The early return happens before FocusOn, so a disabled button must not steal focus on
        // the way to doing nothing.
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);                          // auto-focused
        controller.Add(b);
        controller.Disable(b);

        controller.Press(b);

        Assert.Null(controller.Held);
        Assert.Same(a, controller.Focused);
    }

    [Fact]
    public void Press_WithNothingFocused_HoldsNothing()
    {
        UIController controller = new();
        Button a = new("a");
        controller.Add(a);
        controller.UnFocus();

        controller.Press();

        Assert.Null(controller.Held);
    }

    [Fact]
    public void Release_WithNothingHeld_CommitsNothing()
    {
        UIController controller = new();
        Button a = new("a");
        controller.Add(a);

        bool fired = false;
        controller.OnReleased(a, () => fired = true);

        controller.Release();

        Assert.False(fired);
    }

    // ---- a second press before the first is released ------------------------

    [Fact]
    public void ASecondPress_BeforeRelease_TakesOverTheHold_AndOnlyItCommits()
    {
        // One press at a time: the newer press owns the hold, and the abandoned one does not
        // commit later. Stated here because the fix is about who owns the press.
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        List<string> committed = [];
        controller.OnReleased(a, () => committed.Add("a"));
        controller.OnReleased(b, () => committed.Add("b"));

        controller.Press(a);
        controller.Press(b);

        Assert.Same(b, controller.Held);

        controller.Release();

        Assert.Equal("b", Assert.Single(committed));
    }

    [Fact]
    public void DisablingAnUnheldButton_LeavesTheHoldAlone()
    {
        // Guards against over-correcting: only the held button's own enablement decides its
        // commit, and an unrelated disable must not touch the press in flight.
        UIController controller = new();
        Button a = new("a");
        Button b = new("b");
        controller.Add(a);
        controller.Add(b);

        bool fired = false;
        controller.OnReleased(a, () => fired = true);

        controller.Press(a);
        controller.Disable(b);

        Assert.Same(a, controller.Held);

        controller.Release();

        Assert.True(fired);
    }
}
