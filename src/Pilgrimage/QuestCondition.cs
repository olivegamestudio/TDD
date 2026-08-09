namespace Pilgrimage;

/// <summary>
/// What a quest condition is about. The model names the rule; deciding whether it holds belongs to
/// the presentation, which is the layer that knows what a level or a faction is.
/// </summary>
public enum QuestConditionKind
{
    /// <summary>
    /// The character being played is the one the condition names. This is how a quest is made
    /// character-specific: a quest carrying no condition of this kind is everybody's.
    /// </summary>
    Character,

    /// <summary>
    /// The character's standing with the group the condition names is at least
    /// <see cref="QuestCondition.Threshold"/>.
    /// </summary>
    Faction,

    /// <summary>
    /// The character has reached at least level <see cref="QuestCondition.Threshold"/>.
    /// </summary>
    Level,

    /// <summary>
    /// The quest the condition names has been completed. This is what makes a chain a chain.
    /// </summary>
    QuestPrerequisite,
}

/// <summary>
/// A state that has to <em>hold</em> for a quest to move: which character is being played, what a
/// group makes of them, how far they have come, or what they have already finished.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conditions gate; triggers fire.</b> A <see cref="QuestTrigger"/> is something that
/// <em>happens</em> — the player arrives somewhere — and it moves the quest when it does. A
/// condition never fires on its own; it is asked, and it answers the same way for as long as
/// nothing about the character changes. Collapsing the two into one list would have meant either
/// conditions that pretend to fire or triggers that quietly also mean "and check this".
/// </para>
/// <para>
/// <b>This is data, not behaviour</b>, exactly as <see cref="QuestTrigger"/> is. A level is a game
/// concept and Pilgrimage holds no game knowledge by design, so a condition stores a kind and the
/// values that kind needs, and the game side evaluates it against whatever it is that has levels —
/// the same split <see cref="QuestTrigger"/> already lives by, where the library states a distance
/// and the game measures it.
/// </para>
/// <para>
/// <b>Scope is a condition rather than a field.</b> Design capture had quests carrying a shared
/// versus character-specific scope of their own, and a faction of their own beside it. Both are
/// conditions: a shared quest names no character, a character-specific one names its character, and
/// a quest for two of the four names both. One mechanism instead of three, and it expresses cases a
/// two-valued scope could not.
/// </para>
/// <para>
/// Conditions are built through the factory methods rather than a constructor, so a condition can
/// only ever be one that makes sense. A <see cref="Kind"/> and a bag of loose values would let
/// content author a level requirement that names a faction, and nothing would say so.
/// </para>
/// </remarks>
public sealed record QuestCondition
{
    QuestCondition(QuestConditionKind kind, string subject, int threshold)
    {
        Kind = kind;
        Subject = subject;
        Threshold = threshold;
    }

    /// <summary>
    /// Gets what the condition is about.
    /// </summary>
    public QuestConditionKind Kind { get; }

    /// <summary>
    /// Gets what the condition names — a character, a group or a quest — or an empty string for a
    /// kind that names nothing. Identifiers are never translated: a condition written against a
    /// save in one language has to be read in another.
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the least that will do, for a kind that measures something. Zero for a kind that does
    /// not.
    /// </summary>
    public int Threshold { get; }

    /// <summary>
    /// The quest belongs to one character: it holds only while that character is the one being
    /// played.
    /// </summary>
    /// <param name="characterId">The character's identifier.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">
    /// The identifier names nobody. A condition naming nothing cannot hold for any character, so it
    /// is a quest silently unreachable by everyone — refused where it is authored rather than
    /// discovered by a player who never sees the quest.
    /// </exception>
    public static QuestCondition PlayedAs(string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);

        return new QuestCondition(QuestConditionKind.Character, characterId, 0);
    }

    /// <summary>
    /// The quest belongs to a group: it holds while the character stands at least
    /// <paramref name="standing"/> with them.
    /// </summary>
    /// <param name="groupId">The group's identifier.</param>
    /// <param name="standing">
    /// The standing the character must have reached. Any value is allowed, including a negative
    /// one: a quest a group only offers to somebody they distrust is a quest, and the sign is what
    /// carries the relationship.
    /// </param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">The identifier names no group.</exception>
    public static QuestCondition StandingWith(string groupId, int standing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        return new QuestCondition(QuestConditionKind.Faction, groupId, standing);
    }

    /// <summary>
    /// The quest is for a character who has come far enough: it holds at
    /// <paramref name="level"/> and above.
    /// </summary>
    /// <param name="level">The level the character must have reached.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The level is below one. A character begins at level one, so a condition below it is one
    /// nothing can fail — an author who wrote it meant something, and a condition that quietly does
    /// nothing is worse than one that says it was not understood.
    /// </exception>
    public static QuestCondition AtLeastLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        return new QuestCondition(QuestConditionKind.Level, string.Empty, level);
    }

    /// <summary>
    /// The quest follows another: it holds once the quest it names has been completed.
    /// </summary>
    /// <param name="questId">The identifier of the quest that has to come first.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">
    /// The identifier names no quest. <see cref="QuestLog.Register"/> refuses the same identifiers,
    /// so nothing could ever be registered under one and the condition could never hold.
    /// </exception>
    public static QuestCondition AfterCompleting(string questId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);

        return new QuestCondition(QuestConditionKind.QuestPrerequisite, questId, 0);
    }
}
