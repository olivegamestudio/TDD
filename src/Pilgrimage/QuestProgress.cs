namespace Pilgrimage;

/// <summary>
/// One quest's persisted state. This is the whole of what a quest contributes to a save game;
/// whatever else a game saves alongside it is the game's business.
/// </summary>
/// <remarks>
/// <see cref="QuestLog.Capture"/> writes one of these per quest, but nothing stops a file holding
/// several under one <see cref="QuestId"/> — a hand-edited or merged save can. Which one is taken
/// is decided by <see cref="QuestLog.Restore"/>, not by the order they appear in.
/// </remarks>
/// <param name="QuestId">The identifier of the quest, matching <see cref="QuestDefinition.Id"/>.</param>
/// <param name="State">The state the quest had reached.</param>
public sealed record QuestProgress(string QuestId, QuestState State);
