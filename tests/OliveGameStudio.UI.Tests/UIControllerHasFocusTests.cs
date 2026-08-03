namespace OliveGameStudio.Tests;

/// <summary>
/// Cases for <see cref="IUIController.HasFocus"/> — the question input routing asks before it
/// decides who a frame's input belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Every case here is written against <see cref="IUIController"/> rather than
/// <see cref="UIController"/>, because the caller that needs this is a router holding the
/// interface. <see cref="UIController.Focused"/> has always answered this question for anything
/// holding the concrete type, and it is precisely the consumers that cannot reach it that the
/// member exists for.
/// </para>
/// <para>
/// The states below are all the ways focus arrives and leaves: <see cref="IUIController.Add"/>
/// adopting the first button, <see cref="IUIController.FocusOn"/> aiming it,
/// <see cref="IUIController.UnFocus"/> clearing it, and <see cref="IUIController.Disable"/>
/// re-homing or clearing it as a side effect. A router that reads this wrongly in any one of them
/// sends a frame of input to the wrong place.
/// </para>
/// </remarks>
public sealed class UIControllerHasFocusTests
{
    /// <summary>
    /// Built and handed back as the interface, so nothing below can observe focus through a
    /// member <see cref="IUIController"/> does not promise.
    /// </summary>
    static IUIController Controller() => new UIController();

    /// <summary>
    /// A controller holding nothing has nothing focused. This is the state a router meets before
    /// a screen has built its UI, and the frame of input in it belongs to the ship.
    /// </summary>
    [Fact]
    public void AnEmptyController_HasNoFocus()
    {
        IUIController controller = Controller();

        Assert.False(controller.HasFocus);
    }

    /// <summary>
    /// <see cref="IUIController.Add"/> adopts the first button, so a screen that has built its UI
    /// owns input without having to aim focus itself.
    /// </summary>
    [Fact]
    public void AddingTheFirstButton_TakesFocus()
    {
        IUIController controller = Controller();

        controller.Add(new Button("start"));

        Assert.True(controller.HasFocus);
    }

    /// <summary>
    /// A non-button element is held but never focusable, so adding one does not make the UI the
    /// owner of input. Worth pinning through the interface: the router's whole question is whether
    /// a keypress it is about to hand out belongs to a menu, and an image on screen is not a menu.
    /// </summary>
    [Fact]
    public void AddingANonButtonElement_DoesNotTakeFocus()
    {
        IUIController controller = Controller();

        controller.Add(new Image("logo"));

        Assert.False(controller.HasFocus);
    }

    /// <summary>
    /// The aim reported. <see cref="IUIController.FocusOn"/> is the explicit route in, and the
    /// query has to follow it.
    /// </summary>
    [Fact]
    public void AfterFocusOn_HasFocus()
    {
        IUIController controller = Controller();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        controller.UnFocus();
        controller.FocusOn(options);

        Assert.True(controller.HasFocus);
    }

    /// <summary>
    /// A focus refused is a focus not taken. <see cref="IUIController.FocusOn"/> leaves the
    /// existing focus where it was, so a refusal against an empty controller leaves it empty
    /// rather than reporting a focus aimed at a button nothing holds.
    /// </summary>
    [Fact]
    public void ARefusedFocusOn_DoesNotClaimFocus()
    {
        IUIController controller = Controller();

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("stranger")));

        Assert.False(controller.HasFocus);
    }

    /// <summary>
    /// <see cref="IUIController.UnFocus"/> hands input back. This is the deliberate route a screen
    /// takes to say the UI is not listening.
    /// </summary>
    [Fact]
    public void AfterUnFocus_HasNoFocus()
    {
        IUIController controller = Controller();
        controller.Add(new Button("start"));

        controller.UnFocus();

        Assert.False(controller.HasFocus);
    }

    /// <summary>
    /// Disabling the last enabled button clears focus, and the query says so. This is the route
    /// that reaches no-focus without anyone asking for it — <c>MenuScreen</c> starts here, with its
    /// start button disabled until the save has been read.
    /// </summary>
    [Fact]
    public void DisablingTheLastEnabledButton_LeavesNoFocus()
    {
        IUIController controller = Controller();
        Button start = new("start");
        controller.Add(start);

        controller.Disable(start);

        Assert.False(controller.HasFocus);
    }

    /// <summary>
    /// Disabling the focused button while another is enabled re-homes focus rather than clearing
    /// it, so the UI still owns input. The distinction is the point of the member: "the focused
    /// button was disabled" and "nothing is focused" are different answers to a router.
    /// </summary>
    [Fact]
    public void DisablingTheFocusedButton_WhileAnotherIsEnabled_KeepsFocus()
    {
        IUIController controller = Controller();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        controller.Disable(start);

        Assert.True(controller.HasFocus);
    }

    /// <summary>
    /// The counterpart: enabling a button while nothing is focused adopts it, so input comes back
    /// to the UI the moment a screen has something pressable again.
    /// </summary>
    [Fact]
    public void EnablingWhileNothingIsFocused_TakesFocusBack()
    {
        IUIController controller = Controller();
        Button start = new("start");
        controller.Add(start);
        controller.Disable(start);

        controller.Enable(start);

        Assert.True(controller.HasFocus);
    }

    /// <summary>
    /// Focus is a claim about a button, not about whether it can be pressed. A disabled button that
    /// is deliberately focused still means the UI owns input — <see cref="IUIController.Press"/>
    /// declines the press on its own, and a router that read this as "no focus" would send the
    /// keypress to the ship while a menu is on screen.
    /// </summary>
    [Fact]
    public void AFocusedButtonThatIsDisabled_StillCountsAsFocus()
    {
        IUIController controller = Controller();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        controller.Disable(options);
        controller.FocusOn(options);

        Assert.True(controller.HasFocus);
    }

    /// <summary>
    /// Two controllers answer for themselves. The controller is a singleton in the shipping game,
    /// but two exist at once during a screen transition, and one having focus says nothing about
    /// the other.
    /// </summary>
    [Fact]
    public void OneControllersFocus_IsNotAnothers()
    {
        IUIController menu = Controller();
        IUIController game = Controller();
        menu.Add(new Button("start"));

        Assert.True(menu.HasFocus);
        Assert.False(game.HasFocus);
    }

    /// <summary>
    /// The concrete type's <see cref="UIController.Focused"/> and the interface's
    /// <see cref="IUIController.HasFocus"/> are two readings of one field, and this is the case
    /// that fails if they ever become two answers.
    /// </summary>
    [Fact]
    public void HasFocus_AgreesWithFocused_ThroughEveryTransition()
    {
        UIController controller = new();
        Button start = new("start");
        Button options = new("options");

        Assert.Equal(controller.Focused is not null, controller.HasFocus);

        controller.Add(start);
        Assert.Equal(controller.Focused is not null, controller.HasFocus);

        controller.Add(options);
        controller.FocusOn(options);
        Assert.Equal(controller.Focused is not null, controller.HasFocus);

        controller.UnFocus();
        Assert.Equal(controller.Focused is not null, controller.HasFocus);

        controller.Enable(start);
        Assert.Equal(controller.Focused is not null, controller.HasFocus);

        controller.Disable(start);
        controller.Disable(options);
        Assert.Equal(controller.Focused is not null, controller.HasFocus);
    }
}
