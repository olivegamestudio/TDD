namespace Pilgrimage;

/// <summary>
/// Carries the quest a <see cref="QuestLog"/> event is about, so a listener does not have to work
/// out which of the registered quests changed.
/// </summary>
/// <param name="quest">The quest that started or completed.</param>
public sealed class QuestEventArgs(Quest quest) : EventArgs
{
    /// <summary>
    /// Gets the quest the event is about.
    /// </summary>
    public Quest Quest { get; } = quest;
}
