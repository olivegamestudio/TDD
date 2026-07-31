using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The quests Battle Force 2249 ships with, and where a new game starts the player.
/// </summary>
/// <remarks>
/// Quest 1 opens the game: the player begins inside the collapsing debris field and gets clear of
/// it by flying forward. Its start marker sits on <see cref="PlayerStart"/>, which is what makes it
/// begin on a new game launch without the player doing anything.
/// </remarks>
public sealed class BattleForceCampaign : ICampaign
{
    /// <summary>
    /// The identifier quest 1 is saved under. Save games refer to it, so it must not change.
    /// </summary>
    public const string Quest1Id = "quest-1";

    /// <summary>
    /// The title of quest 1, as shown to the player.
    /// </summary>
    public const string Quest1Title = "Get out. The debris field is collapsing around you.";

    /// <inheritdoc />
    public Position PlayerStart => new(0, 0);

    /// <inheritdoc />
    public IReadOnlyList<QuestDefinition> Quests { get; } =
    [
        new QuestDefinition(
            Quest1Id,
            Quest1Title,
            // on the player's starting position, so the quest auto starts on a new game
            Start: new QuestMarker(new Position(0, 0), 25),
            // 1000 units forward, clear of the debris field
            End: new QuestMarker(new Position(0, 1000), 50)),
    ];
}
