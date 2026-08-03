namespace OliveGameStudio.Input.Tests;

/// <summary>
/// Covers where a frame of input goes, against the real <see cref="UIController"/> rather than a
/// stand-in: focus arrives and leaves by more routes than a caller drives, and the routing rule is
/// written in terms of what focus is doing.
/// </summary>
public sealed class InputRouterTests
{
    readonly UIController _ui = new();
    readonly RoutedShipInput _ship = new();
    readonly Button _start = new("START");
    readonly InputRouter _router;

    public InputRouterTests()
    {
        _router = new InputRouter(_ui, _ship);
    }

    /// <summary>
    /// Puts a focused button in front of the router, which is what a menu screen does.
    /// </summary>
    void ShowTheMenu()
    {
        _ui.Add(_start);
        _ui.FocusOn(_start);
    }

    static InputFrame Keys(
        bool ahead = false,
        bool astern = false,
        bool port = false,
        bool starboard = false,
        bool confirm = false) =>
        new(new KeyboardFrame(ahead, astern, port, starboard, confirm), GamePadFrame.None);

    static InputFrame Stick(
        double thrust = 0,
        double turn = 0,
        bool confirm = false,
        bool connected = true) =>
        new(KeyboardFrame.None, new GamePadFrame(connected, thrust, turn, confirm));

    static InputFrame Both(KeyboardFrame keyboard, GamePadFrame pad) => new(keyboard, pad);

    /// <summary>
    /// Presses and releases confirm on the keyboard against a focused menu, which is how a player
    /// starts the game and therefore how the keyboard becomes the device the game is played on.
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
    public void BeforeAnythingIsPressed_NoDeviceIsChosen()
    {
        Assert.Equal(ControlDevice.None, _router.LockedTo);
    }

    [Fact]
    public void BeforeAnythingIsPressed_TheShipIsHandsOff()
    {
        // the company screen: nothing is focused, so the frame falls through to the ship — but no
        // device has been chosen, so there is nobody whose keys it should be obeying
        _router.Route(Keys(ahead: true));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void TheDeviceThatPressesStart_IsTheOneTheGameIsPlayedOn()
    {
        ShowTheMenu();

        _router.Route(Keys(confirm: true));

        Assert.Equal(ControlDevice.Keyboard, _router.LockedTo);
    }

    [Fact]
    public void ThePadThatPressesStart_IsTheOneTheGameIsPlayedOn()
    {
        ShowTheMenu();

        _router.Route(Stick(confirm: true));

        Assert.Equal(ControlDevice.GamePad, _router.LockedTo);
    }

    [Fact]
    public void TheDeviceIsChosenAtThePress_NotAtTheRelease()
    {
        // the menu commits the start button on release, and the game screen is entered from there.
        // Choosing at the release would leave the frame between the two with no device chosen.
        ShowTheMenu();

        _router.Route(Keys(confirm: true));

        Assert.Equal(ControlDevice.Keyboard, _router.LockedTo);
    }

    [Fact]
    public void ConfirmPressesTheFocusedButton()
    {
        bool pressed = false;
        ShowTheMenu();
        _ui.OnPressed(_start, () => pressed = true);

        _router.Route(Keys(confirm: true));

        Assert.True(pressed);
    }

    [Fact]
    public void LettingGoOfConfirm_CommitsTheButton()
    {
        bool released = false;
        ShowTheMenu();
        _ui.OnReleased(_start, () => released = true);

        _router.Route(Keys(confirm: true));
        Assert.False(released);

        _router.Route(Keys(confirm: false));
        Assert.True(released);
    }

    [Fact]
    public void HoldingConfirm_PressesOnce_NotEveryFrame()
    {
        // a held key arrives every frame for as long as it is held; a button is pressed once. The
        // menu disables its start button as it commits, so a repeat would be reported as pressing
        // a button that is no longer enabled rather than ignored.
        int presses = 0;
        ShowTheMenu();
        _ui.OnPressed(_start, () => presses++);

        for (int frame = 0; frame < 10; frame++)
        {
            _router.Route(Keys(confirm: true));
        }

        Assert.Equal(1, presses);
    }

    [Fact]
    public void AConfirmAlreadyHeldWhenTheMenuAppears_DoesNotPressTheButton()
    {
        // the player is flying with confirm held down when something takes focus. That key was
        // never pressed at this menu, and a menu that acted on it would fire a button the player
        // did not aim at.
        StartOnTheKeyboard();

        _router.Route(Keys(confirm: true));           // held while flying, nothing focused

        bool pressed = false;
        _ui.OnPressed(_start, () => pressed = true);
        _ui.FocusOn(_start);

        _router.Route(Keys(confirm: true));           // still held, now focused

        Assert.False(pressed);
    }

    [Fact]
    public void WhileTheMenuHasFocus_TheShipIsHandsOff()
    {
        // UI first: the same keys work the menu and fly the ship, and a player working the menu is
        // not also flying
        StartOnTheKeyboard();
        _ui.FocusOn(_start);

        _router.Route(Keys(ahead: true));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void WhenNothingIsFocused_TheKeysFlyTheShip()
    {
        StartOnTheKeyboard();

        _router.Route(Keys(ahead: true, starboard: true));

        Assert.Equal(ShipControls.FromKeys(ahead: true, astern: false, port: false, starboard: true),
            _ship.Read());
    }

    [Fact]
    public void WhenNothingIsFocused_TheStickFliesTheShip()
    {
        StartOnTheGamePad();

        _router.Route(Stick(thrust: 1, turn: -1));

        Assert.Equal(ShipControls.FromStick(1, -1, InputRouter.DefaultDeadZone), _ship.Read());
    }

    [Fact]
    public void TheShipIsHandedOffAgain_TheFrameTheMenuComesBack()
    {
        // the ship must not keep flying on the last controls it was given. Nothing clears them on
        // its own, so the router has to write every frame, including the frames it writes nothing.
        StartOnTheKeyboard();
        _router.Route(Keys(ahead: true));
        Assert.False(_ship.Read().IsNeutral);

        _ui.FocusOn(_start);
        _router.Route(Keys(ahead: true));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void ThePadCannotFlyTheShip_OnceTheKeyboardHasTheGame()
    {
        // the lock, in the form the player feels it: a pad left plugged in with something resting
        // on the stick cannot take the ship away from the person flying it
        StartOnTheKeyboard();

        _router.Route(Both(KeyboardFrame.None, new GamePadFrame(true, 1, 1, false)));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void TheKeyboardCannotFlyTheShip_OnceThePadHasTheGame()
    {
        StartOnTheGamePad();

        _router.Route(Both(new KeyboardFrame(true, false, false, false, false), GamePadFrame.None));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void TheOtherDeviceCannotWorkTheMenu_OnceOneHasTheGame()
    {
        // the lock is the control method, not just the flight controls. A second player leaning on
        // a pad should not be able to work the menu of a game somebody else is playing.
        StartOnTheKeyboard();

        bool pressed = false;
        _ui.OnPressed(_start, () => pressed = true);
        _ui.FocusOn(_start);

        _router.Route(Stick(confirm: true));

        Assert.False(pressed);
        Assert.Equal(ControlDevice.Keyboard, _router.LockedTo);
    }

    [Fact]
    public void TheDeviceStaysChosen_HoweverLongTheGameRunsOn()
    {
        StartOnTheKeyboard();

        for (int frame = 0; frame < 100; frame++)
        {
            _router.Route(Stick(thrust: 1, turn: 1, confirm: true));
        }

        Assert.Equal(ControlDevice.Keyboard, _router.LockedTo);
    }

    [Fact]
    public void APadThatIsUnpluggedMidGame_IsHandsOff()
    {
        // the honest consequence of locking for the session: the ship stops rather than being
        // handed to a device the player did not choose
        StartOnTheGamePad();

        _router.Route(Stick(thrust: 1, connected: false));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void APadThatIsNotPluggedIn_CannotBeTheDeviceTheGameIsPlayedOn()
    {
        // a disconnected pad reports what a driver felt like reporting. Taking a confirm from one
        // would lock the game to a device nobody can press anything on.
        ShowTheMenu();

        _router.Route(Stick(confirm: true, connected: false));

        Assert.Equal(ControlDevice.None, _router.LockedTo);
    }

    [Fact]
    public void AStickThatCannotBeRead_IsHandsOff()
    {
        StartOnTheGamePad();

        _router.Route(Stick(thrust: double.NaN, turn: double.NaN));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void ARestingStick_IsHandsOff_AtTheDeadZoneItWasGiven()
    {
        InputRouter router = new(_ui, _ship, deadZone: 0.5);
        ShowTheMenu();
        router.Route(Stick(confirm: true));
        router.Route(Stick(confirm: false));
        _ui.UnFocus();

        router.Route(Stick(thrust: 0.4, turn: -0.4));

        Assert.True(_ship.Read().IsNeutral);
    }

    [Fact]
    public void ThePadIsAskedFirst_WhenBothPressStartOnTheSameFrame()
    {
        // an order rather than a coin toss. A player who has picked up a pad has plainly chosen it,
        // and a keyboard nobody is at cannot press anything, so asking the pad first costs nothing.
        ShowTheMenu();

        _router.Route(Both(
            new KeyboardFrame(false, false, false, false, Confirm: true),
            new GamePadFrame(true, 0, 0, Confirm: true)));

        Assert.Equal(ControlDevice.GamePad, _router.LockedTo);
    }

    [Fact]
    public void RoutingRefusesNoUIController()
    {
        Assert.Throws<ArgumentNullException>(() => new InputRouter(null!, _ship));
    }

    [Fact]
    public void RoutingRefusesNoShipToFly()
    {
        Assert.Throws<ArgumentNullException>(() => new InputRouter(_ui, null!));
    }
}
