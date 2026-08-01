using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The ships Battle Force 2249 has. There is one, and a new game awards it: the fiction gives the
/// Disgraced a single ship and nothing to spare, so the interesting axis is what is bolted to it
/// rather than which hull it is.
/// </summary>
public sealed class BattleForceShipyard : IShipyard
{
    /// <inheritdoc />
    public IReadOnlyList<Ship> Ships { get; } = [DisgracedShip.Ship];

    /// <inheritdoc />
    /// <remarks>
    /// The one ship the game has. When there is a second, this is the decision about which one a
    /// new game starts with, rather than the accident of which one happens to be first.
    /// </remarks>
    public Ship StartingShip => DisgracedShip.Ship;

    /// <inheritdoc />
    public Ship? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : Ships.FirstOrDefault(ship => ship.Id == id);
}
