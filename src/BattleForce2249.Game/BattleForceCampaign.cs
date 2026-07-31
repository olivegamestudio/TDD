using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The quests Battle Force 2249 ships with.
/// </summary>
/// <remarks>
/// Quest 1 opens the game: the player begins inside the collapsing debris field and gets clear of
/// it by flying forward. Both its triggers are proximity triggers — the distances live here, and
/// the markers those distances are measured from live in <see cref="BattleForceWorld"/>.
/// </remarks>
public sealed class BattleForceCampaign : ICampaign
{
    /// <summary>
    /// The identifier quest 1 is saved under. Save games refer to it, so it must not change, and
    /// unlike the title it is never translated.
    /// </summary>
    public const string Quest1Id = "quest-1";

    /// <inheritdoc />
    public IReadOnlyList<QuestDefinition> Quests { get; } =
    [
        new QuestDefinition(
            Quest1Id,
            // in the player's language; the id above stays the same in every language
            GameText.Quest1Title,
            // begins once the player is within 25 units of the start marker, which a new game
            // spawns them on, so the quest starts without the player doing anything
            Start: new QuestTrigger(QuestTriggerKind.Proximity, 25),
            // finishes within 50 units of the exit marker, a wide enough mouth to catch a ship
            // travelling at speed
            End: new QuestTrigger(QuestTriggerKind.Proximity, 50)),
    ];
}
