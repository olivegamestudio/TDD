namespace OliveGameStudio;

/// <summary>
/// Sends a frame of input to the menu or to the ship, and remembers which device the player is on.
/// </summary>
/// <remarks>
/// <para>
/// Two rules, and everything here is one of them. <strong>Routing is UI first</strong>: the frame
/// belongs to the user interface while something there is focused, and to the ship when nothing is.
/// <strong>The device is locked</strong> at the first press the menu accepts, and stays locked for
/// the session.
/// </para>
/// <para>
/// The lock takes the place of the free per-frame arbitration in
/// <see cref="FirstActiveShipInput"/>, which is still the right answer for a game that wants a
/// player to be able to put one device down and pick another up mid-flight. This one does not: the
/// device that started the game is the device it is played on, so a pad left plugged in with a
/// controller resting on the stick cannot take the ship away from somebody flying it on the keys,
/// and a second person cannot work the menu of a game they are not playing.
/// </para>
/// <para>
/// The price is paid when the chosen device goes away — a pad unplugged mid-flight leaves the ship
/// hands off rather than handing it to the keyboard. That is the intended reading of "locked for
/// the session" and not an oversight: silently moving the player onto a device they did not choose
/// is the failure the lock exists to prevent, and the ship stopping is at least visible.
/// </para>
/// </remarks>
public sealed class InputRouter : IInputRouter
{
    /// <summary>
    /// The dead zone applied to a stick when no host has stated its own.
    /// </summary>
    /// <remarks>
    /// A used stick does not return to dead centre, and a game that took the difference for input
    /// would fly itself. The number belongs to the host that knows its own hardware, which is why
    /// it is a constructor argument — but a default here means a game composes and is flyable
    /// before a host has said anything about it, rather than reading a worn stick raw.
    /// </remarks>
    public const double DefaultDeadZone = 0.2;

    readonly IUIController _ui;
    readonly RoutedShipInput _ship;
    readonly RoutedInteraction _interaction;
    readonly double _deadZone;

    /// <summary>
    /// Whether the confirm the router is tracking was held on the previous frame.
    /// </summary>
    /// <remarks>
    /// A press is an edge, and a held key is not: the device reports it held on every frame for as
    /// long as the player holds it, while a button is pressed once. This is the only state the
    /// router keeps between frames, and it is kept whether or not the frame went to the UI — so a
    /// confirm held while flying, on a frame where something then takes focus, is not read as the
    /// player aiming at a button that has only just appeared.
    /// </remarks>
    bool _confirmHeld;

    /// <summary>
    /// Whether the interact the router is tracking was held on the previous frame.
    /// </summary>
    /// <remarks>
    /// Kept for the same reason as <see cref="_confirmHeld"/>, and kept whether or not the frame
    /// went to the UI for a reason this one shows plainly: interact and confirm can be the same
    /// key, so the press that commits the menu's start button is still held on the first frame of
    /// the game. Tracked across the change of focus, that is one press and opens nothing; tracked
    /// only while flying, it would be a fresh press the moment the game began.
    /// </remarks>
    bool _interactHeld;

    /// <summary>
    /// Whether the back-out the router is tracking was held on the previous frame.
    /// </summary>
    bool _cancelHeld;

    /// <summary>
    /// Routes input between the given user interface and the given ship.
    /// </summary>
    /// <param name="ui">The user interface, asked each frame whether it holds focus.</param>
    /// <param name="ship">Where the ship's controls are put for the physics to read.</param>
    /// <param name="interaction">
    /// Where what the player asked for besides flying is put for the frame path to read.
    /// </param>
    /// <param name="deadZone">
    /// How far a stick must travel before it counts as being pushed, from 0 to 1. Defaults to
    /// <see cref="DefaultDeadZone"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// There is no user interface, no ship, or nowhere to put an interaction.
    /// </exception>
    public InputRouter(
        IUIController ui,
        RoutedShipInput ship,
        RoutedInteraction interaction,
        double deadZone = DefaultDeadZone)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(interaction);

        _ui = ui;
        _ship = ship;
        _interaction = interaction;
        _deadZone = deadZone;
    }

    /// <inheritdoc />
    public ControlDevice LockedTo { get; private set; } = ControlDevice.None;

    /// <inheritdoc />
    public void Route(InputFrame frame)
    {
        ControlDevice confirming = Confirming(frame);
        bool confirm = confirming != ControlDevice.None;
        bool interact = Interacting(frame);
        bool cancel = Cancelling(frame);

        // asked, not remembered: Add, Enable and Disable all move focus on their own, so a router
        // that tracked what it last focused would be wrong the frame after any of them
        if (_ui.HasFocus)
        {
            // UI first, here as everywhere. While something on screen holds the controls the only
            // thing besides working it that the player can ask for is to be out of it, so interact
            // is not offered — a conversation cannot open a second conversation.
            _interaction.Interact = false;
            _interaction.Cancel = LockedTo != ControlDevice.None && cancel && !_cancelHeld;

            if (confirm && !_confirmHeld)
            {
                // the press, not the release. The menu commits its start button on release and
                // enters the game screen from there, so choosing at the release would leave the
                // frame in between with no device chosen and the ship briefly unflyable.
                LockedTo = confirming;
                _ui.Press();
            }
            else if (!confirm && _confirmHeld)
            {
                _ui.Release();
            }

            // written every frame, including this one. Nothing clears the ship's controls on its
            // own, so a frame that belongs to the menu has to say so — otherwise the ship flies on
            // whatever it was last given for as long as the menu is up.
            _ship.Controls = ShipControls.Neutral;
        }
        else
        {
            _ship.Controls = ControlsOf(frame);

            // The press, not the hold: a finger resting on the interact key is one request to talk
            // to somebody, not one on every frame it rests there. Nothing offers a way to back out
            // of flying, so the other half is written false rather than left where it was.
            //
            // Nobody asks for anything until a device has been chosen, which is the same answer
            // the ship gets — the game cannot be reached without pressing start, so this is not a
            // state the player can be in rather than a case being handled.
            _interaction.Interact = LockedTo != ControlDevice.None && interact && !_interactHeld;
            _interaction.Cancel = false;
        }

        _confirmHeld = confirm;
        _interactHeld = interact;
        _cancelHeld = cancel;
    }

    /// <summary>
    /// Whether the locked device is asking to interact this frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once a device is locked it is the only one asked, which is the lock being a statement about
    /// the whole control method rather than only about the flight controls: a pad left plugged in
    /// must not be able to talk to somebody on behalf of a player at the keys.
    /// </para>
    /// <para>
    /// Before then <em>any</em> device counts, and that is about the edge rather than about who is
    /// playing. Interact is the same key as confirm on a keyboard and the same button on a pad, so
    /// the press that chooses the device is a press of this too — and a frame that answered "no"
    /// because no device had been chosen yet would make the next frame, on which one has, look
    /// like a fresh press of a key nobody has let go of. Nothing acts on the answer until a device
    /// is locked, so counting the press early costs nothing and stops the game opening a
    /// conversation with whoever the player spawns next to.
    /// </para>
    /// </remarks>
    bool Interacting(InputFrame frame) => LockedTo switch
    {
        ControlDevice.Keyboard => frame.Keyboard.Interact,
        ControlDevice.GamePad => PadIsInteracting(frame),
        _ => PadIsInteracting(frame) || frame.Keyboard.Interact,
    };

    /// <summary>
    /// Whether a pad that is actually plugged in is asking to interact.
    /// </summary>
    static bool PadIsInteracting(InputFrame frame) => frame.GamePad is { Connected: true, Interact: true };

    /// <summary>
    /// Whether the locked device is asking to back out this frame, tracked from before the lock
    /// for the same reason as <see cref="Interacting"/>.
    /// </summary>
    bool Cancelling(InputFrame frame) => LockedTo switch
    {
        ControlDevice.Keyboard => frame.Keyboard.Cancel,
        ControlDevice.GamePad => PadIsCancelling(frame),
        _ => PadIsCancelling(frame) || frame.Keyboard.Cancel,
    };

    /// <summary>
    /// Whether a pad that is actually plugged in is asking to back out.
    /// </summary>
    static bool PadIsCancelling(InputFrame frame) => frame.GamePad is { Connected: true, Cancel: true };

    /// <summary>
    /// Which device is asking to confirm this frame, or <see cref="ControlDevice.None"/> if none is.
    /// </summary>
    /// <remarks>
    /// Once a device is locked it is the only one asked, which is what makes the lock a statement
    /// about the whole control method rather than only about the flight controls. Before then the
    /// pad is asked first: a player who has picked up a pad has plainly chosen it, and a keyboard
    /// nobody is at cannot press anything, so the order costs the keyboard nothing.
    /// </remarks>
    ControlDevice Confirming(InputFrame frame) => LockedTo switch
    {
        ControlDevice.Keyboard => frame.Keyboard.Confirm ? ControlDevice.Keyboard : ControlDevice.None,
        ControlDevice.GamePad => PadIsConfirming(frame) ? ControlDevice.GamePad : ControlDevice.None,
        _ when PadIsConfirming(frame) => ControlDevice.GamePad,
        _ when frame.Keyboard.Confirm => ControlDevice.Keyboard,
        _ => ControlDevice.None,
    };

    /// <summary>
    /// Whether a pad that is actually plugged in is asking to confirm.
    /// </summary>
    /// <remarks>
    /// Connection is checked as well as the button, because a disconnected pad reports whatever the
    /// driver felt like reporting. Taking a confirm from one would lock the game to a device the
    /// player cannot press anything on and has no way to get back off.
    /// </remarks>
    static bool PadIsConfirming(InputFrame frame) => frame.GamePad is { Connected: true, Confirm: true };

    /// <summary>
    /// What the locked device is asking the ship to do this frame.
    /// </summary>
    /// <remarks>
    /// Hands off until a device has been chosen. The game cannot be reached without pressing start,
    /// and pressing start is what chooses the device, so this is not a state the player can fly in
    /// — but it is the honest answer to "which device's keys should the ship obey" when nobody has
    /// said, and it is what keeps the ship still through the company screen.
    /// </remarks>
    ShipControls ControlsOf(InputFrame frame) => LockedTo switch
    {
        ControlDevice.Keyboard => ShipControls.FromKeys(
            frame.Keyboard.Ahead,
            frame.Keyboard.Astern,
            frame.Keyboard.Port,
            frame.Keyboard.Starboard),

        ControlDevice.GamePad => frame.GamePad.Connected
            ? ShipControls.FromStick(frame.GamePad.Thrust, frame.GamePad.Turn, _deadZone)
            : ShipControls.Neutral,

        _ => ShipControls.Neutral,
    };
}
