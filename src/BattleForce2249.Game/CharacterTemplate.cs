using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// A character as content authors them: who they are, the hull they start in, where they start, and
/// what they start with.
/// </summary>
/// <remarks>
/// A template is authored and never changes at runtime; <see cref="Character"/> is the instance
/// played, and it is the instance that earns, spends and loses things. The pairing is the same one
/// <c>QuestDefinition</c> and <c>Quest</c> already use, for the same reason: content that can be
/// changed by play is content nothing can be checked against.
/// </remarks>
/// <param name="Id">
/// The stable identifier the template is known and saved by. It must outlive changes to the name,
/// so it is not derived from one, and it is never translated.
/// </param>
/// <param name="Name">The character's name as shown to the player, in their language.</param>
/// <param name="Ship">The hull they start in.</param>
/// <param name="StartLocation">
/// The identifier of the place a new game begins at. A place, not a coordinate: the world is what
/// turns this into a position, so several quests can mean something at the same named place and a
/// later visit can be told apart from the first.
/// </param>
/// <param name="StartingInventory">What they own before they have earned anything.</param>
public sealed record CharacterTemplate(
    string Id,
    string Name,
    ShipProfile Ship,
    string StartLocation,
    IReadOnlyList<Item> StartingInventory);
