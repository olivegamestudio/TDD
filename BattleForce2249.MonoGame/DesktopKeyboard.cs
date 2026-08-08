using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Reads this host's keyboard.
/// </summary>
/// <remarks>
/// Host wiring, and deliberately nothing more: which keys the game uses is a platform decision,
/// while what a held key <em>means</em> — to a menu or to the ship — is engine code and is covered
/// there. Nothing in this class can be tested, because <see cref="Keyboard.GetState()"/> is a
/// static call into MonoGame with no seam in front of it, so keeping it to the naming of keys is
/// what makes that acceptable.
/// </remarks>
public static class DesktopKeyboard
{
    /// <summary>
    /// Reads the keyboard as it stands now.
    /// </summary>
    /// <returns>What the player is asking for, in the terms the engine understands.</returns>
    /// <remarks>
    /// <para>
    /// Both the WASD cluster and the arrow keys, rather than one or the other. They cost nothing
    /// to bind together, and a player who tries the wrong one first would otherwise conclude the
    /// game does not respond. Confirm is Enter or Space for the same reason.
    /// </para>
    /// <para>
    /// <b>Turning and strafing split the two clusters differently.</b> Q and E turn the ship,
    /// leaving A and D free to strafe — sideways thrust independent of the way the ship is
    /// pointed, which W/S/A/D alone could not ask for once A and D stopped meaning "turn". The
    /// arrow keys are untouched: Left and Right still turn, so the simple four-key scheme they
    /// have always offered keeps meaning exactly what it did before strafing existed.
    /// </para>
    /// </remarks>
    public static KeyboardFrame Read()
    {
        KeyboardState keys = Keyboard.GetState();

        return new KeyboardFrame(
            Ahead: keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up),
            Astern: keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down),
            Port: keys.IsKeyDown(Keys.Q) || keys.IsKeyDown(Keys.Left),
            Starboard: keys.IsKeyDown(Keys.E) || keys.IsKeyDown(Keys.Right),
            Confirm: keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.Space),
            StrafePort: keys.IsKeyDown(Keys.A),
            StrafeStarboard: keys.IsKeyDown(Keys.D));
    }
}
