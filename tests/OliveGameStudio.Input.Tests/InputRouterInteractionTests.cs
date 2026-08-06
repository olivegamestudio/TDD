namespace OliveGameStudio.Input.Tests;

/// <summary>
/// Covers the two things a player can ask for that are not flying: to talk to whoever is in front
/// of them, and to be out of whatever has the controls.
/// </summary>
/// <remarks>
/// These are presses rather than states, which is the whole difference between them and the ship's
/// controls: a held thrust key means thrust on every frame it is held, while a held interact key
/// means one request to talk. The cases here are mostly about that edge, and about the one the
/// shipped bindings make unavoidable — interact and confirm can be the same key, so the press that
/// starts the game is still held on the game's first frame.
/// </remarks>
public sealed class InputRouterInteractionTests
{
    readonly UIController _ui = new();
    readonly RoutedShipInput _ship = new();
    readonly RoutedInteraction _interaction = new();
    readonly Button _start = new("START");
    readonly InputRouter _router;

    public InputRouterInteractionTests()
    {
        _router = new InputRouter(_ui, _ship, _interaction);
    }

    void ShowTheMenu()
    {
        _ui.Add(_start);
        _ui.FocusOn(_start);
    }

    static InputFrame Keys(bool confirm = false, bool interact = false, bool cancel = false) =>
        new(
            new KeyboardFrame(false, false, false, false, confirm, interact, cancel),
            GamePadFrame.None);

    static InputFrame Stick(bool confirm = false, bool interact = false, bool cancel = false) =>
        new(KeyboardFrame.None, new GamePadFrame(true, 0, 0, confirm, interact, cancel));

    static InputFrame NoPad(bool interact = false, bool cancel = false) =>
        new(KeyboardFrame.None, new GamePadFrame(false, 0, 0, false, interact, cancel));

    /// <summary>
    /// Starts the game on the keyboard, which is what locks the device — until then the router has
    /// no device to read an interact from.
    /// </summary>
    void StartOnTheKeyboard()
    {
        ShowTheMenu();
        _router.Route(Keys(confirm: true));
        _router.Route(Keys(confirm: false));
        _ui.UnFocus();
    }

    void StartOnTheGamePad()
    {
        ShowTheMenu();
        _router.Route(Stick(confirm: true));
        _router.Route(Stick(confirm: false));
        _ui.UnFocus();
    }

    [Fact]
    public void BeforeAnythingIsPressed_NothingIsAskedFor()
    {
        Assert.False(_interaction.Interact);
        Assert.False(_interaction.Cancel);
    }

    [Fact]
    public void PressingInteractWhileFlying_AsksToTalk()
    {
        StartOnTheKeyboard();

        _router.Route(Keys(interact: true));

        Assert.True(_interaction.Interact);
    }

    [Fact]
    public void HoldingInteract_AsksOnce()
    {
        // a finger resting on the key is one request to talk to somebody, not one a frame
        StartOnTheKeyboard();
        _router.Route(Keys(interact: true));

        _router.Route(Keys(interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void LettingGoAndPressingAgain_AsksAgain()
    {
        StartOnTheKeyboard();
        _router.Route(Keys(interact: true));
        _router.Route(Keys(interact: false));

        _router.Route(Keys(interact: true));

        Assert.True(_interaction.Interact);
    }

    [Fact]
    public void AFrameNobodyAskedOn_ClearsTheRequest()
    {
        // written every frame, including the ones where nothing was asked: a request left sitting
        // here would be acted on again by whatever read it next
        StartOnTheKeyboard();
        _router.Route(Keys(interact: true));

        _router.Route(Keys());

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void ThePressThatStartedTheGame_DoesNotThenAskToTalk()
    {
        // interact and confirm are the same key on the shipped bindings, so the start press is
        // still held on the first frame of the game. Held across the change of focus, it is one
        // press; tracked only while flying, it would be a fresh one the moment the game began.
        ShowTheMenu();
        _router.Route(Keys(confirm: true, interact: true));
        _router.Route(Keys(confirm: false, interact: false));
        _ui.UnFocus();

        _router.Route(Keys(confirm: true, interact: true));
        bool askedOnTheFirstFrameOfTheGame = _interaction.Interact;

        Assert.True(askedOnTheFirstFrameOfTheGame);
    }

    [Fact]
    public void AStartPressStillHeld_DoesNotOpenAnything()
    {
        ShowTheMenu();
        _router.Route(Keys(confirm: true, interact: true));

        // the menu commits on release; the key is still down as the game begins
        _ui.UnFocus();
        _router.Route(Keys(confirm: true, interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void WhileSomethingHoldsTheControls_InteractIsNotOffered()
    {
        // UI first, here as everywhere: a conversation cannot open a second conversation
        StartOnTheKeyboard();
        _ui.FocusOn(_start);

        _router.Route(Keys(interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void WhileSomethingHoldsTheControls_PressingCancel_AsksToBackOut()
    {
        StartOnTheKeyboard();
        _ui.FocusOn(_start);

        _router.Route(Keys(cancel: true));

        Assert.True(_interaction.Cancel);
    }

    [Fact]
    public void HoldingCancel_AsksOnce()
    {
        StartOnTheKeyboard();
        _ui.FocusOn(_start);
        _router.Route(Keys(cancel: true));

        _router.Route(Keys(cancel: true));

        Assert.False(_interaction.Cancel);
    }

    [Fact]
    public void WhileFlying_ThereIsNothingToBackOutOf()
    {
        StartOnTheKeyboard();

        _router.Route(Keys(cancel: true));

        Assert.False(_interaction.Cancel);
    }

    [Fact]
    public void ACancelHeldFromBeforeTheConversation_DoesNotCloseIt()
    {
        // the key was already down when the conversation took the controls, so no press has
        // happened since; closing on it would shut a conversation the player never read
        StartOnTheKeyboard();
        _router.Route(Keys(cancel: true));

        _ui.FocusOn(_start);
        _router.Route(Keys(cancel: true));

        Assert.False(_interaction.Cancel);
    }

    [Fact]
    public void BeforeADeviceIsChosen_NothingIsAskedFor()
    {
        // the same answer the ship gets: no device has been chosen, so nobody has asked anything
        _router.Route(Keys(interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void OnlyTheLockedDevice_IsAsked()
    {
        // the lock is a statement about the whole control method, not only about the flight
        // controls: a pad left plugged in must not be able to talk to somebody
        StartOnTheKeyboard();

        _router.Route(Stick(interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void ThePad_AsksToTalkWhenItIsTheDevice()
    {
        StartOnTheGamePad();

        _router.Route(Stick(interact: true));

        Assert.True(_interaction.Interact);
    }

    [Fact]
    public void APadThatIsNotPluggedIn_AsksNothing()
    {
        // a disconnected pad reports whatever the driver felt like reporting
        StartOnTheGamePad();

        _router.Route(NoPad(interact: true));

        Assert.False(_interaction.Interact);
    }

    [Fact]
    public void ThePad_BacksOutWhenItIsTheDevice()
    {
        StartOnTheGamePad();
        _ui.FocusOn(_start);

        _router.Route(Stick(cancel: true));

        Assert.True(_interaction.Cancel);
    }

    [Fact]
    public void AskingToTalk_LeavesTheShipFlying()
    {
        // interact is not a flight control and must not become one by accident
        StartOnTheKeyboard();

        _router.Route(new InputFrame(
            new KeyboardFrame(Ahead: true, false, false, false, Confirm: false, Interact: true),
            GamePadFrame.None));

        Assert.Equal(1, _ship.Read().Thrust);
        Assert.True(_interaction.Interact);
    }
}
