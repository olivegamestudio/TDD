// PlayerIndex lives in Microsoft.Xna.Framework, not in .Input beside GamePad — this using is
// load bearing and its absence is a compile error, which is how it was found.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Flies the ship from a gamepad's left stick.
/// </summary>
/// <remarks>
/// <para>
/// The left stick supplies both axes: pushed forward is thrust ahead, pushed sideways is helm. The
/// alternative the ticket raised — triggers for thrust, stick for the helm — was left out. It is a
/// second control scheme rather than a better one, and choosing between schemes is #7's, which
/// needs a settings model and a menu that do not exist yet. One stick is the binding every pad has
/// and mirrors the keyboard's four directions exactly, so it is the one to ship without a chooser.
/// </para>
/// <para>
/// A disconnected pad reports neutral rather than being absent, so it simply loses to whatever
/// else is bound. Nothing has to check whether a controller turned up.
/// </para>
/// </remarks>
public sealed class GamePadShipInput : IShipInput
{
    /// <summary>
    /// How far the stick must move before it is believed.
    /// </summary>
    /// <remarks>
    /// A fifth of the stick's travel. Worn sticks rest measurably off centre, and a ship that
    /// drifts while nobody is touching the pad reads as the game being broken rather than the
    /// hardware being old. Past this the remaining travel still reaches full thrust.
    /// </remarks>
    public const double DeadZone = 0.2;

    /// <inheritdoc />
    public ShipControls Read()
    {
        // GamePadDeadZone.None because the dead zone is applied below: letting MonoGame apply its
        // own as well would zero the stick twice and cost travel the player can feel
        GamePadState pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None);

        if (!pad.IsConnected)
        {
            return ShipControls.Neutral;
        }

        return ShipControls.FromStick(
            // Y is positive up the stick, which is the direction the ship is asked forward in
            thrust: pad.ThumbSticks.Left.Y,
            turn: pad.ThumbSticks.Left.X,
            DeadZone);
    }
}
