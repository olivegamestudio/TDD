namespace Pilgrimage;

/// <summary>
/// The authored description of a quest: what it is called and where it begins and ends. A
/// definition is content and never changes at runtime; the mutable half is <see cref="Quest"/>.
/// </summary>
/// <param name="Id">
/// The stable identifier written to the save game. It must outlive changes to the title, so it is
/// not derived from one.
/// </param>
/// <param name="Title">The quest title as shown to the player.</param>
/// <param name="Start">The marker that begins the quest when reached.</param>
/// <param name="End">The marker that completes the quest when reached.</param>
/// <param name="AutoStarts">
/// Whether reaching <paramref name="Start"/> begins the quest without the player accepting it.
/// </param>
public sealed record QuestDefinition(
    string Id,
    string Title,
    QuestMarker Start,
    QuestMarker End,
    bool AutoStarts = true);
