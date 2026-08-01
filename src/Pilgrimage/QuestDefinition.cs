namespace Pilgrimage;

/// <summary>
/// The authored description of a quest: what it is called, and what begins and finishes it. A
/// definition is content and never changes at runtime; the mutable half is <see cref="Quest"/>.
/// </summary>
/// <param name="Id">
/// The stable identifier written to the save game. It must outlive changes to the title, so it is
/// not derived from one, and it is never translated.
/// </param>
/// <param name="Title">The quest title as shown to the player, in their language.</param>
/// <param name="Start">The trigger that begins the quest.</param>
/// <param name="End">The trigger that completes the quest.</param>
/// <param name="AutoStarts">
/// Whether <paramref name="Start"/> firing begins the quest without the player accepting it.
/// </param>
public sealed record QuestDefinition(
    string Id,
    string Title,
    QuestTrigger Start,
    QuestTrigger End,
    bool AutoStarts = true);
