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
    /// Gets whether the game can resume from this snapshot, which is a narrower question than
    /// whether it could be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only thing that can make a well formed snapshot unplayable today is where it says the
    /// player is. See <see cref="IsAPositionTheGameCanDraw"/> for why a position can be perfectly
    /// good arithmetic and still be nowhere the game can put anybody.
    /// </para>
    /// <para>
    /// This is deliberately the only place the game asks the question, so there is one answer to
    /// change. PR #49 is settling a second, stricter far edge on a parallel stack —
    /// <c>Position.MaxCoordinate</c>, at 2^52, where a world unit stops being a step a
    /// <see cref="double"/> can take — and that bound is well inside this one. When the two land
    /// together this should defer to it rather than state a second edge beside it.
    /// </para>
    /// </remarks>
    public bool CanBeResumed =>
        IsAPositionTheGameCanDraw(PlayerX) && IsAPositionTheGameCanDraw(PlayerY);

    /// <summary>
    /// States whether a saved coordinate is one the game could put the player at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world is measured in <see cref="double"/> and the screen in <see cref="float"/>, and
    /// the game narrows from one to the other once, where it hands a position to the drawing side
    /// (<c>GameScreen.PoseOf</c>). Everything from there on — the ship's pose, the camera's target,
    /// every sprite transformed through it — is a <see cref="float"/>.
    /// </para>
    /// <para>
    /// So a coordinate past <see cref="float.MaxValue"/> is not a corrupt number. It is finite, it
    /// parses, it round trips, and the world model carries it without complaint; it becomes an
    /// infinity at the moment it is narrowed, and a position that is nowhere is the whole world
    /// drawn nowhere. That is a game nobody can play, which is why it is refused with the saves
    /// that cannot be read rather than resumed into.
    /// </para>
    /// <para>
    /// The bound is exactly where the drawing side runs out and not a step short of it: the
    /// furthest the game can draw from is still somewhere a player is allowed to have got to.
    /// </para>
    /// </remarks>
    /// <param name="coordinate">The saved coordinate, on either axis.</param>
    /// <returns><c>true</c> when the game could draw the world from there.</returns>
    public static bool IsAPositionTheGameCanDraw(double coordinate) =>
        float.IsFinite((float)coordinate);

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
