using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Decides whether a quest's conditions hold for the character being played.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of a gated quest that Pilgrimage deliberately does not own. A
/// <see cref="QuestCondition"/> is data — a kind, and the values that kind needs — and a level, a
/// faction and a character template are all game concepts the library holds no knowledge of. So the
/// model declares the rule and this applies it, exactly as <see cref="QuestProximityWatcher"/>
/// measures the distance a <see cref="QuestTrigger"/> only states.
/// </para>
/// <para>
/// Everything a condition can ask about hangs off the <see cref="Character"/>: which template is
/// being played, what each group makes of them, how far they have come, and what they have
/// finished. That is not a coincidence — it is pillar 4's split doing its job. Conditions are read
/// off the persistent record rather than off the ship, so a quest the player had earned their way
/// into is still open to them after they lose the hull they earned it in.
/// </para>
/// </remarks>
public static class QuestConditions
{
    /// <summary>
    /// Determines whether every one of a quest's conditions holds.
    /// </summary>
    /// <remarks>
    /// A list means all of them, and an empty list holds: a quest nobody gated is one anybody can
    /// play, which is what makes conditions something content adds rather than something every
    /// quest has to opt out of.
    /// </remarks>
    /// <param name="conditions">The conditions to test.</param>
    /// <param name="character">The character being played.</param>
    /// <returns><c>true</c> when all of them hold.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    public static bool Hold(IEnumerable<QuestCondition> conditions, Character character)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(character);

        return conditions.All(condition => Holds(condition, character));
    }

    /// <summary>
    /// Determines whether one condition holds.
    /// </summary>
    /// <param name="condition">The condition to test.</param>
    /// <param name="character">The character being played.</param>
    /// <returns><c>true</c> when it holds.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The condition is of a kind this build does not know how to ask. Every kind
    /// <see cref="QuestCondition"/> can be built as is answered here, so reaching this means a kind
    /// was added to the model and nothing was taught to evaluate it — a quest silently available to
    /// everybody, or to nobody, is exactly the failure the guards in the model exist to prevent, so
    /// it is raised rather than guessed at.
    /// </exception>
    public static bool Holds(QuestCondition condition, Character character)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(character);

        return condition.Kind switch
        {
            // ordinal, because identifiers are not text: a template id is matched the way it was
            // written, not the way the player's culture would compare two words
            QuestConditionKind.Character =>
                string.Equals(character.Template.Id, condition.Subject, StringComparison.Ordinal),

            // a group the character has never dealt with stands at zero, so a quest wanting
            // friendship is shut to a stranger and one wanting only neutrality is open to them
            QuestConditionKind.Faction =>
                character.Reputation.With(condition.Subject) >= condition.Threshold,

            QuestConditionKind.Level =>
                character.Progression.Level >= condition.Threshold,

            // a prerequisite naming a quest this build no longer ships does not hold. That is the
            // safe answer of the two: a chain whose first link has been dropped stays shut rather
            // than opening a quest the player was never meant to reach yet.
            QuestConditionKind.QuestPrerequisite =>
                character.Quests.Find(condition.Subject) is { IsCompleted: true },

            _ => throw new ArgumentOutOfRangeException(
                nameof(condition),
                condition.Kind,
                $"'{condition.Kind}' is not a quest condition this build knows how to ask."),
        };
    }
}
