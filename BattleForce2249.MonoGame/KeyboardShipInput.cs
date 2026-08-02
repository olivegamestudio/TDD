using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Flies the ship from the desktop keyboard.
/// </summary>
/// <remarks>
/// <para>
/// The bindings are here rather than in the engine because which key means "ahead" is a decision
/// about this game on this platform, while turning a held key into an axis is not. The engine owns
/// the second half, in <see cref="ShipControls.FromKeys"/>, and this class owns nothing but the
/// choice of keys.
/// </para>
/// <para>
/// Both W/A/S/D and the arrow keys are bound, either hand alone. They are read as one set rather
/// than as two schemes, so a player using one and then the other needs no setting changed — the
/// scheme *selection* asked for in #7 is a separate piece of work with a settings model behind it.
/// </para>
/// </remarks>
public sealed class KeyboardShipInput : IShipInput
{
    /// <inheritdoc />
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
