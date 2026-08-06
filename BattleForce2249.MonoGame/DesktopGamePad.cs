// PlayerIndex lives in Microsoft.Xna.Framework, not in .Input alongside everything else here.
// Load bearing: without it this file does not compile, and it reads like a stray using.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Reads this host's gamepad.
/// </summary>
/// <remarks>
/// Host wiring only, for the same reason as <see cref="DesktopKeyboard"/>: what a stick position
/// means to the ship is <c>ShipControls.FromStick</c> and is covered there, while
/// <see cref="GamePad.GetState(PlayerIndex, GamePadDeadZone)"/> has no seam in front of it.
/// <para>
/// The left stick carries both axes rather than the triggers carrying thrust. Triggers are a
/// second control scheme rather than a better one, and choosing between schemes needs a settings
/// model and a menu that do not exist yet. One stick is the binding every pad has, and it mirrors
/// the keyboard's four directions exactly.
/// </para>
/// </remarks>
public static class DesktopGamePad
{
    /// <summary>
    /// How far the stick must travel before the game takes it as being pushed.
    /// </summary>
    /// <remarks>
    /// A used stick does not return to dead centre, and without this the ship would fly itself.
    /// The number is stated here, by the host that knows the hardware, and handed to the engine
    /// once at composition rather than applied by the driver — so there is one number the tests
    /// can pin instead of whatever the platform decided.
    /// </remarks>
    public const double DeadZone = 0.2;

    /// <summary>
    /// Reads the pad as it stands now.
    /// </summary>
    /// <returns>The pad's axes and buttons, raw, for the engine to interpret.</returns>
    /// <remarks>
    /// Read with MonoGame's own dead zone turned off, because the game applies its own. The axes
    /// are passed on untouched: taking the dead zone off here would leave two places that decide
    /// how much slop to ignore, and the engine is where the stick becomes a ship's controls.
    /// </remarks>
    public static GamePadFrame Read()
    {
        GamePadState pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None);

        if (!pad.IsConnected)
        {
            return GamePadFrame.None;
        }

        // Y is up on the stick and ahead is positive thrust, so the axes map straight across.
        //
        // A interacts as well as confirms, which is the pad's version of the space bar doing both:
        // the same button means "yes" to whatever is on screen and "talk to them" when nothing is.
        // B backs out, which is where every pad in this genre puts it, and it is bound to nothing
        // else so that leaving a conversation is one button that always means leave.
        return new GamePadFrame(
            Connected: true,
            Thrust: pad.ThumbSticks.Left.Y,
            Turn: pad.ThumbSticks.Left.X,
            Confirm: pad.Buttons.A == ButtonState.Pressed || pad.Buttons.Start == ButtonState.Pressed,
            Interact: pad.Buttons.A == ButtonState.Pressed,
            Cancel: pad.Buttons.B == ButtonState.Pressed);
    }
}
