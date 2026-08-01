using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The persisted snapshot of a game in progress: where the player is, what they fly, and how far
/// they have got with each quest. This is the shape written to the save file, so changing it
/// changes what older saves can be read back into.
/// </summary>
public sealed record SaveGame
{
    /// <summary>
    /// Gets the player's position along the X axis.
    /// </summary>
    public double PlayerX { get; init; }

    /// <summary>
    /// Gets the player's position along the Y axis.
    /// </summary>
    public double PlayerY { get; init; }

    /// <summary>
    /// Gets the id of the ship the player was flying.
    /// </summary>
    /// <remarks>
    /// Recorded rather than re-derived on every load. Pillar 4 asks that the persistent record
    /// survive death, and which ship the player has earned is part of that record the moment
    /// there is more than one to earn — a save that re-awarded the starting hull would quietly
    /// take a better one away. Blank on a save written before ships were recorded, which the
    /// session reads as "give them the starting ship".
    /// </remarks>
    public string ShipId { get; init; } = "";

    /// <summary>
    /// Gets the state of every quest that was registered when the game was saved.
    /// </summary>
    public IReadOnlyList<QuestProgress> Quests { get; init; } = [];

    /// <summary>
    /// Compares two snapshots by value, including the quests. The compiler generated version would
    /// compare the quest list by reference, which would make two identical saves unequal.
    /// </summary>
    /// <param name="other">The snapshot to compare with.</param>
    /// <returns><c>true</c> when both snapshots hold the same progress.</returns>
    public bool Equals(SaveGame? other) =>
        other is not null
        && PlayerX.Equals(other.PlayerX)
        && PlayerY.Equals(other.PlayerY)
        && ShipId == other.ShipId
        && Quests.SequenceEqual(other.Quests);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PlayerX, PlayerY, ShipId, Quests.Count);
}
