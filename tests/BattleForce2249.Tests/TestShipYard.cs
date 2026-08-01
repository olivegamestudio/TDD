using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A yard stocking two ships, so that "the ship the save named" and "the ship a new game awards"
/// are different answers.
/// </summary>
/// <remarks>
/// The shipping yard has one ship, and against it both answers look identical — a session that
/// ignored the save and re-awarded the starting ship every time would pass. Every session test
/// that touches the ship uses this rather than <see cref="BattleForceShipYard"/> for that reason.
/// </remarks>
public sealed class TestShipYard : IShipYard
{
    /// <summary>
    /// The ship a new game awards.
    /// </summary>
    public static readonly Ship Starter =
        new("starter", "starter-art", new ShipHandling(Acceleration: 100, Drag: 1, TurnRate: 2));

    /// <summary>
    /// A better ship, standing in for one the player has come by since — the case the save exists
    /// to protect.
    /// </summary>
    public static readonly Ship Earned =
        new("earned", "earned-art", new ShipHandling(Acceleration: 300, Drag: 1, TurnRate: 3));

    /// <inheritdoc />
    public IReadOnlyList<Ship> Ships { get; } = [Starter, Earned];

    /// <inheritdoc />
    public Ship StartingShip => Starter;

    /// <inheritdoc />
    public Ship? Find(string shipId) =>
        Ships.FirstOrDefault(ship => string.Equals(ship.Id, shipId, StringComparison.Ordinal));
}
