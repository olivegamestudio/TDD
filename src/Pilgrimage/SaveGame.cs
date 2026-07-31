namespace Pilgrimage;

/// <summary>
/// The persisted snapshot of a game in progress: where the player is and how far they have got
/// with each quest. This is the shape written to the save file, so changing it changes what older
/// saves can be read back into.
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
        && Quests.SequenceEqual(other.Quests);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PlayerX, PlayerY, Quests.Count);
}

/// <summary>
/// One quest's entry in a <see cref="SaveGame"/>.
/// </summary>
/// <param name="QuestId">The identifier of the quest, matching <see cref="QuestDefinition.Id"/>.</param>
/// <param name="State">The state the quest had reached.</param>
public sealed record QuestProgress(string QuestId, QuestState State);
