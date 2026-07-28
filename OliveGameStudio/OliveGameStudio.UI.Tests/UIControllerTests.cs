namespace OliveGameStudio.Tests;

public sealed class UIControllerTests
{
    [Fact]
    public void FirstButtonAdded_BecomesFocused()
    {
        UIController controller = new();
        Button start = new();
        controller.Add(start);

        Button? pressed = null;
        controller.Pressed += (_, e) => pressed = e.Button;
        controller.Press();

        Assert.Same(start, pressed);            // Add gave it focus → Press fired it
    }

    [Fact]
    public void LaterButtonsAdded_DoNotStealFocus()
    {
        UIController controller = new();
        Button first = new();
        controller.Add(first);
        controller.Add(new Button());           // later — must not steal focus

        Button? pressed = null;
        controller.Pressed += (_, e) => pressed = e.Button;
        controller.Press();

        Assert.Same(first, pressed);
    }

    [Fact]
    public void FocusOn_OverridesTheDefault()
    {
        UIController controller = new();
        controller.Add(new Button());           // auto-focused default
        Button chosen = new();
        controller.Add(chosen);
        controller.FocusOn(chosen);             // override

        Button? pressed = null;
        controller.Pressed += (_, e) => pressed = e.Button;
        controller.Press();

        Assert.Same(chosen, pressed);
    }

    [Fact]
    public void Press_FiresTheFocused_NotItsSiblings()
    {
        UIController controller = new();
        Button start = new();
        Button options = new();
        controller.Add(start);                  // auto-focused
        controller.Add(options);                // sibling — unfocused

        Button? pressed = null;
        controller.Pressed += (_, e) => pressed = e.Button;
        controller.Press();

        Assert.Same(start, pressed);            // the focused one — not options
    }

    [Fact]
    public void Press_OnEmptyController_FiresNothing()
    {
        UIController controller = new();        // nothing added — nothing to focus

        int fired = 0;
        controller.Pressed += (_, _) => fired++;
        controller.Press();

        Assert.Equal(0, fired);
    }
}
