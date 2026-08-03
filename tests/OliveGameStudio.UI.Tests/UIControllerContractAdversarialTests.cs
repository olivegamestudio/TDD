namespace OliveGameStudio.Tests;

/// <summary>
/// QA's second adversarial pass over #101, standing where the consumers stand.
/// </summary>
/// <remarks>
/// <para>
/// This pull request was sent back once because <see cref="IUIController"/> — not
/// <see cref="UIController"/> — still described the behaviour the issue removed, and that mattered
/// precisely because <c>GameScreen</c> and <c>MenuScreen</c> take the interface rather than the
/// concrete type. The fix is a doc fix, and a doc fix is the one kind of change nothing can fail
/// on. So the gap the round trip exposed is not that the comment was wrong; it is that **no test
/// in this suite binds to <see cref="IUIController"/> at all**, so the contract of record was free
/// to drift away from the type implementing it without anything going red.
/// </para>
/// <para>
/// These cases are deliberately written against the interface and observe only what the interface
/// exposes. It has no <c>Focused</c>, no <c>Held</c> and no <c>IsEnabled</c>, so <em>which</em>
/// button focus ended up on is read the way a consumer has to read it — by pressing and seeing
/// which action fires. <see cref="IUIController.HasFocus"/> answers whether there is a focus at
/// all and is exercised in <see cref="UIControllerHasFocusTests"/>; it deliberately does not name
/// the button, so it is no help to the cases below. That is a weaker instrument than the concrete
/// tests use, and it is the right one here: it fails if the promise the interface makes stops
/// being true for the object handed to a screen.
/// </para>
/// </remarks>
public sealed class UIControllerContractAdversarialTests
{
    /// <summary>
    /// Built and handed back as the interface, never as the concrete type, so nothing below can
    /// reach a member <see cref="IUIController"/> does not promise.
    /// </summary>
    static IUIController Controller() => new UIController();

    // ---- the exception the interface now documents ----

    /// <summary>
    /// The promise added to <see cref="IUIController.FocusOn"/> by this pull request, asserted
    /// through the interface that makes it. A screen holding an <see cref="IUIController"/> has no
    /// way to ask whether a button is managed, so the throw is the whole of what it gets.
    /// </summary>
    [Fact]
    public void FocusOn_AStranger_ThrowsThroughTheInterface()
    {
        IUIController controller = Controller();
        Button managed = new("start");
        controller.Add(managed);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("elsewhere")));

        Assert.Contains("elsewhere", error.Message);
    }

    /// <summary>
    /// The same, for the case the interface calls out by name: a stranger sharing a managed
    /// button's name. The interface's own preamble says a button it does not hold is a stranger
    /// even when the name matches exactly, and <see cref="IUIController.FocusOn"/> now repeats it.
    /// </summary>
    [Fact]
    public void FocusOn_AStrangerSharingAManagedName_ThrowsThroughTheInterface()
    {
        IUIController controller = Controller();
        controller.Add(new Button("BACK"));

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("BACK")));
    }

    /// <summary>
    /// "Implementations that refuse leave the existing focus where it was", read the only way a
    /// consumer can read it: press afterwards and see that the button focus was already on is the
    /// one that fires.
    /// </summary>
    [Fact]
    public void FocusOn_ARefusal_LeavesTheFocusAConsumerWouldPress()
    {
        IUIController controller = Controller();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        int started = 0;
        int optioned = 0;
        controller.OnPressed(start, () => started++);
        controller.OnPressed(options, () => optioned++);

        controller.FocusOn(options);
        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("options")));

        controller.Press();

        Assert.Equal(0, started);
        Assert.Equal(1, optioned);
    }

    // ---- the sentence this pull request removed from Press ----

    /// <summary>
    /// <see cref="IUIController.Press"/> used to say it was where a focus aimed at an unmanaged
    /// button surfaced. That route is gone, and this is the assertion that it is: a refused aim
    /// leaves nothing behind for a later press to trip over.
    /// </summary>
    /// <remarks>
    /// The removed sentence is the kind of claim that can only be checked by the absence of a
    /// throw, so it needs a press that would have thrown before the guard existed — one taken
    /// after an aim at a stranger, with no intervening successful aim to paper over it.
    /// </remarks>
    [Fact]
    public void Press_ResolvesAfterARefusedAim_RatherThanReportingItLate()
    {
        IUIController controller = Controller();
        Button start = new("start");
        controller.Add(start);

        int fired = 0;
        controller.OnPressed(start, () => fired++);

        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("start")));

        controller.Press();

        Assert.Equal(1, fired);
    }

    /// <summary>
    /// The same claim after focus has been cleared and re-taken, since <see cref="IUIController"/>
    /// exposes <see cref="IUIController.UnFocus"/> and a consumer may leave focus empty. A press
    /// with nothing focused does nothing rather than throwing, and a refused aim in between does
    /// not change that.
    /// </summary>
    [Fact]
    public void Press_WithNothingFocused_DoesNothing_EvenAfterARefusedAim()
    {
        IUIController controller = Controller();
        Button start = new("start");
        controller.Add(start);

        int fired = 0;
        controller.OnPressed(start, () => fired++);

        controller.UnFocus();
        Assert.Throws<InvalidOperationException>(() => controller.FocusOn(new Button("start")));

        controller.Press();

        Assert.Equal(0, fired);
    }

    // ---- the bound of the guard, through the interface ----

    /// <summary>
    /// The interface states the bound as well as the rule: only membership is checked, so a managed
    /// button that is disabled may still be focused. Observed the consumer's way — the aim is
    /// accepted, the press it would commit is declined while disabled, and enabling it makes the
    /// same focus pressable without anything having to re-aim.
    /// </summary>
    /// <remarks>
    /// This is the shape <c>MenuScreen</c> depends on, which is why it is worth pinning at the
    /// interface: the start button is disabled until the save has been read.
    /// </remarks>
    [Fact]
    public void FocusOn_AManagedButtonThatIsDisabled_IsAcceptedThroughTheInterface()
    {
        IUIController controller = Controller();
        Button start = new("start");
        Button options = new("options");
        controller.Add(start);
        controller.Add(options);

        int fired = 0;
        controller.OnPressed(options, () => fired++);

        controller.Disable(options);
        controller.FocusOn(options);

        controller.Press();
        Assert.Equal(0, fired);

        controller.Enable(options);
        controller.Press();
        Assert.Equal(1, fired);
    }

    // ---- the route that does not check, and does not need to ----

    /// <summary>
    /// <see cref="IUIController.Add"/> is the one route to focus that does not call the membership
    /// check — it assigns focus directly to the button it has just taken on. The claim on
    /// <see cref="IUIController.Press"/> that focus can never be a stranger therefore rests on this
    /// route being safe by construction rather than by a guard, which is worth an assertion of its
    /// own: whatever <c>Add</c> focuses is something the controller now holds and can press.
    /// </summary>
    [Fact]
    public void Add_FocusesOnlyAButtonItHasTakenOn()
    {
        IUIController controller = Controller();
        Button first = new("first");
        Button second = new("second");

        int firstFired = 0;
        int secondFired = 0;

        controller.Add(first);
        controller.Add(second);
        controller.OnPressed(first, () => firstFired++);
        controller.OnPressed(second, () => secondFired++);

        // the first button added took focus, and it is pressable rather than a name held in a field
        controller.Press();

        Assert.Equal(1, firstFired);
        Assert.Equal(0, secondFired);
    }

    /// <summary>
    /// A second controller is a second set of buttons, which is the case the whole identity rule
    /// exists for: one controller is shared between screens in the shipping game, so this stands in
    /// for two of them existing at once during a transition. Neither will take the other's button,
    /// and neither is disturbed by the other's refusal.
    /// </summary>
    [Fact]
    public void TwoControllers_RefuseEachOthersButtons_AndNeitherLosesItsOwnFocus()
    {
        IUIController menu = Controller();
        IUIController game = Controller();

        Button menuBack = new("BACK");
        Button gameBack = new("BACK");
        menu.Add(menuBack);
        game.Add(gameBack);

        int menuFired = 0;
        int gameFired = 0;
        menu.OnPressed(menuBack, () => menuFired++);
        game.OnPressed(gameBack, () => gameFired++);

        Assert.Throws<InvalidOperationException>(() => menu.FocusOn(gameBack));
        Assert.Throws<InvalidOperationException>(() => game.FocusOn(menuBack));

        menu.Press();
        game.Press();

        Assert.Equal(1, menuFired);
        Assert.Equal(1, gameFired);
    }
}
