namespace Pilgrimage;

/// <summary>
/// The content a game supplies to the quest system: where a new game puts the player, and the
/// quests that exist in it. Pilgrimage owns the quest machinery; a game owns the quests, so the
/// game implements this.
/// </summary>
public interface ICampaign
{
    /// <summary>
    /// Gets the position a brand new game starts the player at. Any quest meant to auto start on
    /// launch has its start marker placed within reach of it.
    /// </summary>
    Position PlayerStart { get; }

    /// <summary>
    /// Gets every quest in the campaign, each with a unique <see cref="QuestDefinition.Id"/>.
    /// </summary>
    IReadOnlyList<QuestDefinition> Quests { get; }
}
