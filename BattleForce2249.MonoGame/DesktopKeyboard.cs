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
    /// Both the WASD cluster and the arrow keys, rather than one or the other. They cost nothing
    /// to bind together, and a player who tries the wrong one first would otherwise conclude the
    /// game does not respond. Confirm is Enter or Space for the same reason.
    /// <para>
    /// Interact is the space bar the design capture names, with E beside it for the same reason the
    /// arrow keys sit beside WASD. It is deliberately the <em>same</em> key as confirm: routing is
    /// UI first, so a press while flying reaches whoever the player has flown up to and a press
    /// while reading what they said turns the page. Escape backs out, and it is bound to nothing
    /// else — the way out of a conversation has to be one press that always means that.
    /// </para>
    /// </remarks>
    public static KeyboardFrame Read()
    {
        KeyboardState keys = Keyboard.GetState();

        return new KeyboardFrame(
            Ahead: keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up),
            Astern: keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down),
            Port: keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left),
            Starboard: keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right),
            Confirm: keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.Space),
            Interact: keys.IsKeyDown(Keys.Space) || keys.IsKeyDown(Keys.E),
            Cancel: keys.IsKeyDown(Keys.Escape));
    }
}
