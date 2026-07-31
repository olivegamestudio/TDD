namespace Pilgrimage;

/// <summary>
/// One quest's persisted state. This is the whole of what a quest contributes to a save game;
/// whatever else a game saves alongside it is the game's business.
/// </summary>
/// <param name="QuestId">The identifier of the quest, matching <see cref="QuestDefinition.Id"/>.</param>
/// <param name="State">The state the quest had reached.</param>
public sealed record QuestProgress(string QuestId, QuestState State);
