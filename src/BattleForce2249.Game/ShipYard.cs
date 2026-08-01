using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The ships a game ships with: which one a new game awards, and how a ship named in a save game is
/// found again.
/// </summary>
/// <remarks>
/// This exists because of the save, not because of the fiction. A save records the ship by
/// identifier — a ship is content, and writing it into the save whole would freeze the player's
/// ship at whatever it was worth the day they saved — so loading needs somewhere that turns that
/// identifier back into a ship. It sits game side for the same reason <see cref="ICampaign"/> and
/// <see cref="IWorld"/> do: the engine says what a ship is and ships none.
/// </remarks>
public interface IShipYard
{
    /// <summary>
    /// Gets every ship the game has, in no particular order.
    /// </summary>
    IReadOnlyList<Ship> Ships { get; }

    /// <summary>
    /// Gets the ship a brand new game awards the player.
    /// </summary>
    Ship StartingShip { get; }

    /// <summary>
    /// Finds a ship by the identifier it is saved under. Matching is exact: a near miss out of a
    /// hand-edited save is a miss, because case folding or trimming here would put a hull in the
    /// cockpit that nothing ever awarded.
    /// </summary>
    /// <param name="shipId">The identifier read back from a save game.</param>
    /// <returns>
    /// The ship, or <c>null</c> when this build has no ship by that identifier — a save written
    /// before ships were recorded, or by a build that shipped a hull this one does not.
    /// </returns>
    Ship? Find(string shipId);
}

/// <summary>
/// The ships Battle Force 2249 ships with. There is one: the Disgraced flies a single hull, and the
/// axis the game varies is equipment rather than ships.
/// </summary>
public sealed class BattleForceShipYard : IShipYard
{
    /// <inheritdoc />
    /// <remarks>
    /// A list of one rather than a special case for the only ship, so adding the second is adding
    /// a line here.
    /// </remarks>
    public IReadOnlyList<Ship> Ships { get; } = [BattleForceShip.Ship];

    /// <inheritdoc />
    /// <remarks>
    /// The Disgraced begins in the ship they were left with, which is also the only one the game
    /// currently has.
    /// </remarks>
    public Ship StartingShip => BattleForceShip.Ship;

    /// <inheritdoc />
    public Ship? Find(string shipId) =>
        Ships.FirstOrDefault(ship => string.Equals(ship.Id, shipId, StringComparison.Ordinal));
}
