namespace OliveGameStudio.Tests;

public sealed class UIControllerTests
{
    // ---- focus / add ----

    [Fact]
    public void FirstButtonAdded_BecomesFocused()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        Button? pressed = null;
        controller.OnReleased(start, () => pressed = start);
        controller.Press();
        controller.Release();

        Assert.Same(start, pressed);
    }

    [Fact]
    public void LaterButtonsAdded_DoNotStealFocus()
    {
        UIController controller = new();
        Button first = new("first");
        Button second = new("second");
        controller.Add(first);
        controller.Add(second);

        Button? pressed = null;
        controller.OnPressed(first, () => pressed = first);
        controller.OnPressed(second, () => pressed = second);
        controller.Press();
        controller.Release();

        Assert.Same(first, pressed);
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

        Button? pressed = null;
        controller.OnReleased(first, () => pressed = first);
        controller.OnReleased(chosen, () => pressed = chosen);
        controller.Press();
        controller.Release();

        Assert.Same(chosen, pressed);
    }

    [Fact]
    public void Press_FiresTheFocused_NotItsSiblings()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.OnPressed(options, () => pressed = options);
        controller.Press();
        controller.Release();

        Assert.Same(start, pressed);
    }

    [Fact]
    public void PressRelease_OnEmptyController_FiresNothing()
    {
        UIController controller = new();

        controller.Press();
        controller.Release();       // no focus, must not throw or fire
    }

    [Fact]
    public void DisabledFirstButton_LeavesNothingFocused()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);

        bool fired = false;
        controller.OnPressed(start, () => fired = true);
        controller.Press();
        controller.Release();

        Assert.False(fired);
    }

    [Fact]
    public void EnablingWhileNothingFocused_AdoptsFocus()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);
        controller.Enable(start);

        Button? pressed = null;
        controller.OnReleased(start, () => pressed = start);
        controller.Press();
        controller.Release();

        Assert.Same(start, pressed);
    }

    // ---- two-phase press / release / cancel ----

    [Fact]
    public void PressDown_Alone_DoesNotFire()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);
        controller.Press();

        Assert.False(fired);
    }

    [Fact]
    public void Release_Commits()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnPressed(start, () => fired = true);
        controller.Press();
        controller.Release();

        Assert.True(fired);
    }

    [Fact]
    public void Cancel_AbortsWithoutFiring()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        bool fired = false;
        controller.OnReleased(start, () => fired = true);
        controller.Press();
        controller.Cancel();

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
        Assert.Null(controller.Held);            // cleared on release
    }

    [Fact]
    public void Focused_ExposesTheFocusedButton()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        Assert.Same(start, controller.Focused);
        controller.FocusOn(options);
        Assert.Same(options, controller.Focused);
    }

    // ---- mouse / touch through Press(button) ----

    [Fact]
    public void MouseDownUp_OnButton_Commits()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.OnPressed(options, () => pressed = options);
        controller.Press(options);      // down over options
        controller.Release();           // up over options

        Assert.Same(options, pressed);
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
        controller.Press(options);
        controller.Cancel();

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
        controller.OnPressed(locked, () => fired = true);
        controller.Press(locked);
        controller.Release();

        Assert.False(fired);
    }
}
