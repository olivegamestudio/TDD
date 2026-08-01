using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The player's ship on screen. The view holds no opinion about how the ship got where it is:
/// game logic sets <see cref="Pose"/>, and the view draws whatever it last said.
/// </summary>
/// <remarks>
/// This is the seam between the logic and engine stages for the ship. Flight physics can land
/// tested and headless on one side of it, and this can draw with no idea that physics exists on
/// the other; the only thing they agree about is a position and a heading.
/// </remarks>
public interface IShipView : IRenderable
{
    /// <summary>
    /// Where the ship is drawn and which way it points. Set once per frame by whatever moves
    /// the ship; a pose left alone simply draws the ship where it was.
    /// </summary>
    ShipPose Pose { get; set; }
}
