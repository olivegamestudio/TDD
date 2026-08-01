using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The ships a build ships, and the one a new game hands the player.
/// </summary>
/// <remarks>
/// The seam exists for the save. A save records which ship the player owns by id, so resuming a
/// game means turning an id back into a ship — and the session should not have to know the game's
/// ships to do it, any more than it knows the game's quests.
/// </remarks>
public interface IShipyard
{
    /// <summary>
    /// Gets the ship a brand new game awards the player.
    /// </summary>
    ShipModel StartingShip { get; }

    /// <summary>
    /// Gets every ship this build knows how to put a player in.
    /// </summary>
    IReadOnlyList<ShipModel> Ships { get; }

    /// <summary>
    /// Finds a ship by the id a save recorded.
    /// </summary>
    /// <param name="id">The ship id to look for. Compared exactly; ids are never translated.</param>
    /// <returns>
    /// The ship, or <c>null</c> when this build no longer ships it. A caller resuming a save is
    /// expected to fall back rather than fail — the same tolerance a save's quest list gets.
    /// </returns>
    ShipModel? Find(string? id);
}

/// <summary>
/// The ships of Battle Force 2249. One, for now: you are the Disgraced, and the fiction gives you
/// exactly one hull and very little else.
/// </summary>
public sealed class BattleForceShipyard : IShipyard
{
    /// <inheritdoc />
    public ShipModel StartingShip => DisgracedShip.Model;

    /// <inheritdoc />
    public IReadOnlyList<ShipModel> Ships { get; } = [DisgracedShip.Model];

    /// <inheritdoc />
    public ShipModel? Find(string? id) =>
        id is null
            ? null
            : Ships.FirstOrDefault(ship => string.Equals(ship.Id, id, StringComparison.Ordinal));
}
