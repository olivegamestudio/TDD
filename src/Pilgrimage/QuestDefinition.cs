namespace Pilgrimage;

/// <summary>
/// The authored description of a quest: what it is called, what begins and finishes it, what has to
/// be true before either can happen, and how hard it is meant to be. A definition is content and
/// never changes at runtime; the mutable half is <see cref="Quest"/>.
/// </summary>
/// <remarks>
/// <b>Conditions gate and triggers fire</b>, and a quest carries both at each end: it reads as
/// <em>"available once you are level five and have finished the last one; starts when you reach the
/// marker"</em>. See <see cref="QuestCondition"/> for why the two are not one list.
/// </remarks>
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
    bool AutoStarts = true)
{
    readonly IReadOnlyList<QuestCondition> _startConditions = [];
    readonly IReadOnlyList<QuestCondition> _endConditions = [];
    readonly int _level = 1;

    /// <summary>
    /// Gets what has to hold before the quest can begin. Empty by default: a quest nobody gated is
    /// one anybody can start.
    /// </summary>
    /// <remarks>
    /// <b>Every condition has to hold.</b> A list means all of them, which is what lets a quest be
    /// gated on three things at once without the model growing a field per kind, and is what an
    /// author will assume by default. An either-or gate is not expressible and is not being built
    /// for now — a rule everyone assumes is worth more than one nobody needs yet.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The list is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The list holds an entry that is not there.</exception>
    public IReadOnlyList<QuestCondition> StartConditions
    {
        get => _startConditions;
        init => _startConditions = Authored(value, nameof(StartConditions));
    }

    /// <summary>
    /// Gets what has to hold before the quest can be completed. Empty by default.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="StartConditions"/> because turn-in is not the same question as
    /// availability: a quest that opened to a friendly character can require them to have stayed
    /// friendly, and a quest with nothing to say about it carries no end conditions and completes on
    /// its trigger alone.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The list is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The list holds an entry that is not there.</exception>
    public IReadOnlyList<QuestCondition> EndConditions
    {
        get => _endConditions;
        init => _endConditions = Authored(value, nameof(EndConditions));
    }

    /// <summary>
    /// Gets how hard the quest is meant to be, as a level. One unless the content says otherwise.
    /// </summary>
    /// <remarks>
    /// This is the indicative difficulty the player is shown beside a quest, and it is deliberately
    /// <em>not</em> a gate: a level requirement is a <see cref="QuestCondition.AtLeastLevel"/>
    /// condition, which content may or may not author beside this. Danger gates the space and
    /// content gates the story — a quest above the player's weight is one they are allowed to
    /// attempt and told about, not one they are refused.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The level is below one. Levels start at one, so a quest below it is describing nothing.
    /// </exception>
    public int Level
    {
        get => _level;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _level = value;
        }
    }

    /// <summary>
    /// Takes a copy of an authored condition list, having checked it can be read.
    /// </summary>
    /// <remarks>
    /// Copied rather than held, because a definition is content that "never changes at runtime" and
    /// a list handed in is a list the caller can still add to — which would also let an entry past
    /// the check below after it had been made. The check is here rather than at evaluation for the
    /// reason every content guard in this library is: an entry that is not there fails where the
    /// quest is authored, naming the quest, rather than on the frame that happens to ask.
    /// </remarks>
    static IReadOnlyList<QuestCondition> Authored(IReadOnlyList<QuestCondition> conditions, string name)
    {
        ArgumentNullException.ThrowIfNull(conditions, name);

        QuestCondition[] copy = [.. conditions];

        if (copy.Any(condition => condition is null))
        {
            throw new ArgumentException("A quest's conditions hold an entry that is not there.", name);
        }

        return copy;
    }
}
