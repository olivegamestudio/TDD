// PlayerIndex lives in Microsoft.Xna.Framework, not in .Input alongside everything else here.
// Load bearing: without it this file does not compile, and it reads like a stray using.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Flies the ship from a gamepad's left stick.
/// </summary>
/// <remarks>
/// Host wiring only, for the same reason as <see cref="KeyboardShipInput"/>: what a stick position
/// means to the ship is <see cref="ShipControls.FromStick"/> and is covered there, while
/// <see cref="GamePad.GetState(PlayerIndex, GamePadDeadZone)"/> has no seam in front of it.
/// <para>
/// The left stick carries both axes rather than the triggers carrying thrust. Triggers are a
/// second control scheme rather than a better one, and choosing between schemes needs a settings
/// model and a menu that do not exist yet (#7). One stick is the binding every pad has, and it
/// mirrors the keyboard's four directions exactly.
/// </para>
/// </remarks>
public sealed class GamePadShipInput : IShipInput
{
    /// <summary>
    /// How far the stick must travel before the game takes it as being pushed.
    /// </summary>
    /// <remarks>
    /// A used stick does not return to dead centre, and without this the ship would fly itself.
    /// Worse, since the pad is asked before the keyboard, a drifting stick that counted as being
    /// used would shut the keyboard out entirely and leave the player unable to fly at all.
    /// </remarks>
    public const double DeadZone = 0.2;

    /// <inheritdoc />
    /// <remarks>
    /// Read with MonoGame's own dead zone turned off, because the game applies its own — one
    /// stated number the tests can pin, rather than whatever the platform decided. A pad that is
    /// not plugged in reports hands off, so the keyboard behind it answers instead.
    /// </remarks>
    public ShipControls Read()
    {
        GamePadState pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None);

        if (!pad.IsConnected)
        {
            return ShipControls.Neutral;
        }

        // Y is up on the stick and ahead is positive thrust, so the axes map straight across.
        return ShipControls.FromStick(pad.ThumbSticks.Left.Y, pad.ThumbSticks.Left.X, DeadZone);
    }
}
