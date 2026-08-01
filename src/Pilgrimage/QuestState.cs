namespace Pilgrimage;

/// <summary>
/// The point a quest has reached in its lifecycle. This value is written to the save game, so it
/// is persisted by name rather than by ordinal.
/// </summary>
public enum QuestState
{
    /// <summary>The quest exists but the player has not begun it.</summary>
    NotStarted,

    /// <summary>The quest has begun and has not yet been completed.</summary>
    Active,

    /// <summary>The quest has been completed and cannot be started again.</summary>
    Completed,
}
