namespace Pilgrimage;

/// <summary>
/// The point a quest has reached in its lifecycle. This value is written to the save game, so it
/// is persisted by name rather than by ordinal.
/// </summary>
/// <remarks>
/// The members are declared in the order a quest passes through them, and that order is load
/// bearing: <see cref="QuestStateExtensions.IsBehind"/> reads it to decide which of two states is
/// further on, and <see cref="QuestLog.Restore"/> resolves a save that names one quest twice by
/// keeping the furthest. Reordering them therefore changes behaviour — but not saves, which carry
/// the name and never the ordinal.
/// </remarks>
public enum QuestState
{
    /// <summary>The quest exists but the player has not begun it.</summary>
    NotStarted,

    /// <summary>The quest has begun and has not yet been completed.</summary>
    Active,

    /// <summary>The quest has been completed and cannot be started again.</summary>
    Completed,
}
