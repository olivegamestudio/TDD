using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The ships a game ships, and which of them a new player is given. The seam a game supplies its
/// hulls through, the way <see cref="Pilgrimage.ICampaign"/> supplies its quests.
/// </summary>
/// <remarks>
/// This exists because a save names a ship by id and something has to turn that id back into a
/// ship. Hard-coding the starting ship in the session would work today, with one hull, and would
/// have to be unpicked the first time the player earns a second.
/// </remarks>
public interface IShipyard
{
    /// <summary>
    /// Gets every ship the build ships.
    /// </summary>
    IReadOnlyList<Ship> Ships { get; }

    /// <summary>
    /// Gets the ship a new game awards the player.
    /// </summary>
    Ship Starting { get; }

    /// <summary>
    /// Finds a ship by the id it was saved under.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <returns>
    /// The ship, or <c>null</c> when this build no longer ships it. The caller decides what to do
    /// about that; a save the build has drifted from is a thing to tolerate, not to crash on.
    /// </returns>
    Ship? Find(string id);
}

/// <summary>
/// The ships Battle Force 2249 ships with. One hull for now — the Disgraced flies what they were
/// left with.
/// </summary>
public sealed class BattleForceShipyard : IShipyard
{
    /// <inheritdoc />
    public IReadOnlyList<Ship> Ships { get; } = [DisgracedShip.Starting];

    /// <inheritdoc />
    public Ship Starting => DisgracedShip.Starting;

    /// <inheritdoc />
    public Ship? Find(string id) => Ships.FirstOrDefault(ship => ship.Id == id);
}
