using Microsoft.Xna.Framework.Input;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Reads this host's mouse.
/// </summary>
/// <remarks>
/// Host wiring only, for the same reason as <see cref="DesktopKeyboard"/>: what a pointer in a
/// given place <em>means</em> is <c>PointerDrag</c> and whatever laid the screen out, and is
/// covered there, while <see cref="Mouse.GetState()"/> is a static call into MonoGame with no seam
/// in front of it. Keeping this to reading and naming is what makes that acceptable.
/// <para>
/// The left button and no other. A second button means a second meaning, and neither a context
/// menu nor a quick-equip has been designed — see <see cref="MouseFrame"/>.
/// </para>
/// </remarks>
public static class DesktopMouse
{
    /// <summary>
    /// Reads the mouse as it stands now.
    /// </summary>
    /// <returns>Where the pointer is and whether it is holding something.</returns>
    /// <remarks>
    /// A desktop always has a mouse, so this always reports one present. The distinction
    /// <see cref="MouseFrame.Present"/> draws is for hosts that do not — it is the engine's
    /// question rather than this host's, and answering it honestly here costs nothing.
    /// <para>
    /// The position is passed on as the platform gives it, including when the pointer has been
    /// dragged outside the window. A drag has to follow the pointer past the edge of the frame for
    /// the same reason the touch overlay's helm follows a thumb past the rim of its circle: there
    /// is nothing there for the player to feel, so clipping it would put the item down somewhere
    /// they did not choose.
    /// </para>
    /// </remarks>
    public static MouseFrame Read()
    {
        MouseState mouse = Mouse.GetState();

        return new MouseFrame(
            Present: true,
            X: mouse.X,
            Y: mouse.Y,
            PrimaryHeld: mouse.LeftButton == ButtonState.Pressed);
    }
}
