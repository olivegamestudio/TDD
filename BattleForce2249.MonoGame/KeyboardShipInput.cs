using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Flies the ship from the keyboard.
/// </summary>
/// <remarks>
/// Host wiring, and deliberately nothing more: which keys the game uses is a platform decision,
/// while what a held key <em>means</em> to the ship is engine code in
/// <see cref="ShipControls.FromKeys"/> and is covered there. Nothing in this class can be tested —
/// <see cref="Keyboard.GetState()"/> is a static call into MonoGame with no seam in front of it —
/// so keeping it to the naming of keys is what makes that acceptable.
/// </remarks>
public sealed class KeyboardShipInput : IShipInput
{
    /// <inheritdoc />
    /// <remarks>
    /// Both the WASD cluster and the arrow keys, rather than one or the other. They cost nothing
    /// to bind together, and a player who tries the wrong one first would otherwise conclude the
    /// game does not respond.
    /// </remarks>
    public ShipControls Read()
    {
        KeyboardState keys = Keyboard.GetState();

        return ShipControls.FromKeys(
            ahead: keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up),
            astern: keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down),
            port: keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left),
            starboard: keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right));
    }
}
