using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The persisted snapshot of a game in progress: where the player is and how far they have got
/// with each quest. This is the shape written to the save file, so changing it changes what older
/// saves can be read back into.
/// </summary>
/// <remarks>
/// A snapshot with no quests holds an empty list, never a null one, and the type enforces that
/// where the list is set rather than where it is read. Anything else leaves every reader — equality,
/// hashing, serialisation — defending itself against a state the declaration says cannot happen,
/// and the first reader to forget throws instead of comparing.
/// </remarks>
public sealed record SaveGame
{
    readonly IReadOnlyList<QuestProgress> _quests = [];

    /// <summary>
    /// Gets the player's position along the X axis.
    /// </summary>
    public double PlayerX { get; init; }

    /// <summary>
    /// Gets the player's position along the Y axis.
    /// </summary>
    public double PlayerY { get; init; }

    /// <summary>
    /// Gets the state of every quest that was registered when the game was saved. A save with no
    /// quests is an empty list, so setting this to <c>null</c> — as a save read from a file saying
    /// <c>"Quests": null</c> does — stores no quests rather than the absence of the list.
    /// </summary>
    public IReadOnlyList<QuestProgress> Quests
    {
        get => _quests;
        init => _quests = value ?? [];
    }

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
