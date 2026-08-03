using Pilgrimage;

namespace BattleForce2249;

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
    /// Whether the game can actually be resumed at this position — a narrower question than
    /// whether the file could be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the one place the repository states how far out a saved position may be.</b>
    /// Anything else that needs the edge should ask here rather than restate it, because two
    /// statements of the same bound is one of them going stale unnoticed.
    /// </para>
    /// <para>
    /// The edge is the range the drawing side can hold, and it is there because
    /// <c>GameScreen.PoseOf</c> narrows the world's <see cref="double"/> coordinates to the
    /// <see cref="float"/> a graphics device uses. A coordinate past <see cref="float.MaxValue"/>
    /// is a perfectly good <see cref="double"/> — it parses, it round trips, every quest beside it
    /// is intact — and becomes an infinity the instant it is narrowed. <c>ICamera.Target</c>
    /// refuses an infinity outright, so such a save would throw once per frame from the frame
    /// loop, which is far too late to be any use to anybody.
    /// </para>
    /// <para>
    /// It is deliberately <em>not</em> a world size. The world is unbounded and a long flight has
    /// to load, so this refuses only what cannot be drawn at all: <see cref="float.MaxValue"/>
    /// still resumes and <c>1e300</c> does not. The check is here, where the number arrives from a
    /// file, rather than beside the narrowing, because a save can be declined quietly and a frame
    /// cannot.
    /// </para>
    /// </remarks>
    public bool CanBeResumed =>
        float.IsFinite((float)PlayerX) && float.IsFinite((float)PlayerY);

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
