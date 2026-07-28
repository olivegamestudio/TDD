namespace OliveGameStudio.Tests;

public sealed class UIControllerTests
{
    [Fact]
    public void FirstButtonAdded_BecomesFocused()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.Press();

        Assert.Same(start, pressed);            // Add gave it focus -> Press fired its action
    }

    [Fact]
    public void LaterButtonsAdded_DoNotStealFocus()
    {
        UIController controller = new();
        Button first = new("first");
        Button second = new("second");
        controller.Add(first);
        controller.Add(second);                 // later -> must not steal focus

        Button? pressed = null;
        controller.OnPressed(first, () => pressed = first);
        controller.OnPressed(second, () => pressed = second);
        controller.Press();

        Assert.Same(first, pressed);
    }

    [Fact]
    public void FocusOn_OverridesTheDefault()
    {
        UIController controller = new();
        Button first = new("first");
        Button chosen = new("chosen");
        controller.Add(first);                  // auto-focused default
        controller.Add(chosen);
        controller.FocusOn(chosen);             // override

        Button? pressed = null;
        controller.OnPressed(first, () => pressed = first);
        controller.OnPressed(chosen, () => pressed = chosen);
        controller.Press();

        Assert.Same(chosen, pressed);
    }

    [Fact]
    public void Press_FiresTheFocused_NotItsSiblings()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);                  // auto-focused
        controller.Add(options);                // sibling -> unfocused

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.OnPressed(options, () => pressed = options);
        controller.Press();

        Assert.Same(start, pressed);            // the focused one -> not options
    }

    [Fact]
    public void Press_OnEmptyController_FiresNothing()
    {
        UIController controller = new();        // nothing added -> nothing to focus

        controller.Press();                     // must not throw or fire
    }

    [Fact]
    public void DisabledFirstButton_LeavesNothingFocused()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);              // focused -> re-home; none enabled -> clears

        bool fired = false;
        controller.OnPressed(start, () => fired = true);
        controller.Press();

        Assert.False(fired);                    // no selection while waiting
    }

    [Fact]
    public void EnablingWhileNothingFocused_AdoptsFocus()
    {
        UIController controller = new();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);              // waiting...
        controller.Enable(start);               // service ready -> adopts focus

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.Press();

        Assert.Same(start, pressed);            // now focused and pressable
    }

    [Fact]
    public void Press_WithButton_FocusesAndFiresTheClicked()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);                  // start auto-focused
        controller.Add(options);

        Button? pressed = null;
        controller.OnPressed(start, () => pressed = start);
        controller.OnPressed(options, () => pressed = options);
        controller.Press(options);              // mouse click on the unfocused one

        Assert.Same(options, pressed);          // focused + fired options, not start
    }

    [Fact]
    public void Press_WithDisabledButton_DoesNothing()
    {
        UIController controller = new();
        Button start = new("start");
        Button locked = new("locked");
        controller.Add(start);
        controller.Add(locked);
        controller.Disable(locked);

        bool firedLocked = false;
        controller.OnPressed(locked, () => firedLocked = true);
        controller.Press(locked);               // mouse click on a DISABLED button

        Assert.False(firedLocked);              // RED without the enabled guard in Press
    }
}
