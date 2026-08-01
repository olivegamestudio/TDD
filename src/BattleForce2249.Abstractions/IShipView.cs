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
    /// Which sprite the ship is drawn with, as the content build names it.
    /// </summary>
    /// <remarks>
    /// Settable rather than fixed so the picture can follow the ship the player was actually
    /// awarded, the day there is more than one to award. Whatever owns the player's ship sets
    /// this from the ship's own asset key; naming the asset at the draw site instead would mean
    /// every new ship needing a change here. Changing it takes effect on the next draw.
    /// </remarks>
    string AssetKey { get; set; }

    /// <summary>
    /// Where the ship is drawn and which way it points. Set once per frame by whatever moves
    /// the ship; a pose left alone simply draws the ship where it was.
    /// </summary>
    ShipPose Pose { get; set; }
}
