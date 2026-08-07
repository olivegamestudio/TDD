// This project does not turn on implicit usings, so the list the touches are gathered into needs
// naming here. Load bearing: without it this file does not compile.
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input.Touch;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Reads this host's touch screen.
/// </summary>
/// <remarks>
/// Host wiring, for the same reason as <see cref="DesktopKeyboard"/>: what a finger in a given
/// place <em>means</em> is <c>TouchOverlay</c> and is covered there, while
/// <see cref="TouchPanel.GetState()"/> is a static call into MonoGame with no seam in front of it.
/// Keeping this to reading and naming is what makes that acceptable.
/// <para>
/// The desktop is not the platform this device belongs to, and most machines running this build
/// will report nothing here for the life of the session. It is read anyway because a touch screen
/// on a desktop is a thing that exists, and because the alternative is a host that quietly cannot
/// see a device the engine understands.
/// </para>
/// </remarks>
public static class DesktopTouch
{
    /// <summary>
    /// Reads the fingers that are on the screen now.
    /// </summary>
    /// <returns>Every finger still down, in the terms the engine understands.</returns>
    /// <remarks>
    /// A finger the platform reports as released is left out. MonoGame reports a lift once, as a
    /// last state for a touch that is already over, and passing it on would leave the overlay
    /// holding a helm nobody has hold of for one more frame — which is a ship that keeps flying
    /// after the player let go.
    /// </remarks>
    public static TouchFrame Read()
    {
        TouchCollection panel = TouchPanel.GetState();

        if (panel.Count == 0)
        {
            return TouchFrame.None;
        }

        List<TouchPoint> touches = new(panel.Count);

        foreach (TouchLocation touch in panel)
        {
            if (touch.State == TouchLocationState.Released)
            {
                continue;
            }

            touches.Add(new TouchPoint(touch.Id, touch.Position.X, touch.Position.Y));
        }

        return new TouchFrame(touches);
    }
}
