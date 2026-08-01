namespace Pilgrimage;

/// <summary>
/// The quests a game supplies to the quest system. Pilgrimage owns the quest machinery; a game
/// owns the quests, so the game implements this.
/// </summary>
/// <remarks>
/// Quests only. Where the markers are, and where a new game puts the player, are world facts
/// rather than quest facts, and belong to the game alongside the rest of its world.
/// </remarks>
public interface ICampaign
{
    /// <summary>
    /// Gets every quest in the campaign, each with a unique <see cref="QuestDefinition.Id"/>.
    /// </summary>
    IReadOnlyList<QuestDefinition> Quests { get; }
}
