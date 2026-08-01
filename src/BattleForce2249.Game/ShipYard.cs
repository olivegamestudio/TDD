using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The ships a game ships with: which one a new game awards, and how a ship named in a save game is
/// found again.
/// </summary>
/// <remarks>
/// This exists because of the save, not because of the fiction. A save records the ship by
/// identifier — a ship is content and must not be written into the save file whole, or every
/// balance change would have to be applied to saved games too — so loading needs somewhere that
/// turns that identifier back into a ship.
/// </remarks>
public interface IShipYard
{
    /// <summary>
    /// Gets the ship a brand new game awards the player.
    /// </summary>
    Ship StartingShip { get; }

    /// <summary>
    /// Finds a ship by the identifier it is saved under.
    /// </summary>
    /// <param name="shipId">The identifier read back from a save game.</param>
    /// <returns>
    /// The ship, or <c>null</c> when this build has no ship by that identifier — a save written by
    /// a build that shipped one this one does not.
    /// </returns>
    Ship? Find(string shipId);
}

/// <summary>
/// The ships Battle Force 2249 ships with. There is one: the Disgraced flies a single hull, and the
/// axis the game varies is equipment rather than ships.
/// </summary>
public sealed class BattleForceShipYard : IShipYard
{
    /// <summary>
    /// Every ship in the game, in no particular order. A list of one rather than a special case for
    /// the only ship, so adding the second is adding a line here.
    /// </summary>
    public static IReadOnlyList<Ship> Ships { get; } = [DisgracedShip.Ship];

    /// <inheritdoc />
    /// <remarks>
    /// The Disgraced begins with the ship they were left with, which is also the only one the game
    /// currently has.
    /// </remarks>
    public Ship StartingShip => DisgracedShip.Ship;

    /// <inheritdoc />
    public Ship? Find(string shipId) => Ships.FirstOrDefault(ship => ship.Id == shipId);
}
