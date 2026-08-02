namespace Pilgrimage;

/// <summary>
/// The player's quests: every quest the campaign registered, and the state each has reached. It
/// republishes their events, so anything that cares about quest progress — saving, and eventually
/// the quest display — subscribes here rather than to each quest.
/// </summary>
/// <remarks>
/// The log holds no world state and is never updated per frame. Whatever watches the world starts
/// and completes quests through the <see cref="Quest"/> objects it hands out.
/// </remarks>
public sealed class QuestLog
{
    readonly Dictionary<string, Quest> _quests = [];

    /// <summary>
    /// Occurs when any registered quest begins.
    /// </summary>
    public event EventHandler<QuestEventArgs>? QuestStarted;

    /// <summary>
    /// Occurs when any registered quest completes.
    /// </summary>
    public event EventHandler<QuestEventArgs>? QuestCompleted;

    /// <summary>
    /// Gets every registered quest, in the order it was registered.
    /// </summary>
    public IReadOnlyCollection<Quest> Quests => _quests.Values;

    /// <summary>
    /// Gets the quests that have begun and are not yet finished.
    /// </summary>
    public IEnumerable<Quest> Active => _quests.Values.Where(quest => quest.IsActive);

    /// <summary>
    /// Gets the quests the player has completed.
    /// </summary>
    public IEnumerable<Quest> Completed => _quests.Values.Where(quest => quest.IsCompleted);

    /// <summary>
    /// Adds a quest to the log.
    /// </summary>
    /// <param name="definition">The quest to register.</param>
    /// <returns>The runtime quest created for the definition.</returns>
    /// <exception cref="ArgumentException">
    /// <para>
    /// The identifier names no quest — it is <c>null</c>, empty or nothing but whitespace. This is
    /// the door that makes <see cref="Restore"/>'s skip safe. <see cref="Restore"/> passes over an
    /// entry whose identifier names nothing, on the stated grounds that nothing is registered under
    /// one; refusing here is what makes that a fact rather than an assumption. Without it a
    /// campaign could register a quest, play it, save it through <see cref="Capture"/> and have
    /// <see cref="Restore"/> throw the progress away, reporting nothing at any step.
    /// </para>
    /// <para>
    /// Only an identifier made of nothing but whitespace is refused. One with whitespace in it, or
    /// around it, names something and is a campaign's to choose — the library does not trim,
    /// case-fold or otherwise have opinions about identifiers it can store and find again.
    /// </para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A quest with the same identifier is already registered. Two quests sharing an identifier
    /// would overwrite each other in the save game. Note that <see cref="Restore"/> deliberately
    /// does <em>not</em> refuse a duplicate: it reads a file rather than a campaign, and two
    /// entries under one identifier are one quest described twice rather than two quests. The
    /// reasoning is on <see cref="Restore"/>.
    /// </exception>
    public Quest Register(QuestDefinition definition)
    {
        // Asked before the duplicate check because it is the more basic complaint: an identifier
        // that names nothing is not a candidate for being a second anything.
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new ArgumentException(
                "A quest must be registered under an identifier that names something. An identifier that is null, empty or only whitespace is one Restore skips, so a quest registered under it would be saved and then silently lost.",
                nameof(definition));
        }

        if (_quests.ContainsKey(definition.Id))
        {
            throw new ArgumentException($"A quest with the id '{definition.Id}' is already registered.", nameof(definition));
        }

        Quest quest = new(definition);
        quest.Started += OnQuestStarted;
        quest.Completed += OnQuestCompleted;
        _quests.Add(quest.Id, quest);

        return quest;
    }

    /// <summary>
    /// Finds a registered quest by its identifier.
    /// </summary>
    /// <param name="id">The identifier to look for.</param>
    /// <returns>The quest, or <c>null</c> when no quest is registered under that identifier.</returns>
    public Quest? Find(string id) => _quests.GetValueOrDefault(id);

    /// <summary>
    /// Empties the log, detaching from the quests it held so a discarded quest can no longer
    /// republish through it. Used when a new game replaces the one in progress.
    /// </summary>
    public void Clear()
    {
        foreach (Quest quest in _quests.Values)
        {
            quest.Started -= OnQuestStarted;
            quest.Completed -= OnQuestCompleted;
        }

        _quests.Clear();
    }

    /// <summary>
    /// Takes the state of every registered quest, for persisting.
    /// </summary>
    /// <remarks>
    /// One entry per quest is load bearing rather than incidental: <see cref="Restore"/> reads a
    /// second entry naming a quest as a file that was hand-edited or merged, and resolves it on
    /// that basis. A change here that emitted a quest more than once would undercut that reasoning
    /// silently.
    /// </remarks>
    /// <returns>One entry per registered quest.</returns>
    public IReadOnlyList<QuestProgress> Capture() =>
        [.. _quests.Values.Select(quest => new QuestProgress(quest.Id, quest.State))];

    /// <summary>
    /// Puts the registered quests back into their saved states, raising no events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Saves and campaigns drift apart over a game's life, and both directions are tolerated: a
    /// quest the save knows about but this build no longer ships is ignored, and a quest added
    /// since the save was written simply starts from the beginning.
    /// </para>
    /// <para>
    /// <b>An entry naming no quest is drift of the same kind and is skipped.</b> An identifier that
    /// is <c>null</c>, empty or blank names a quest exactly as poorly as one that is merely
    /// unknown: nothing is registered under it — <see cref="Register"/> refuses exactly these, which
    /// is what makes that a fact rather than a hope — so there is nothing to apply it to, and
    /// refusing the batch over it would throw away the progress standing beside it, which is the
    /// part that was real. An entry that is <c>null</c> is not drift; no <see cref="Capture"/> wrote it, so
    /// the batch is refused instead. A game reading its own save file draws that same line one step
    /// earlier, and two edges answering differently about one entry is how a save that needed a
    /// single line skipping came to be discarded whole.
    /// </para>
    /// <para>
    /// <b>A save naming one quest more than once restores it to the furthest of the states it
    /// names.</b> A later entry is applied only when it carries the quest further on; one that
    /// would hand progress back is dropped. So a file saying a quest is both completed and never
    /// started restores it completed, whichever line comes first.
    /// </para>
    /// <para>
    /// That rule is chosen over first-wins or last-wins because it is the only one whose answer
    /// does not depend on the order the entries happen to be in. <see cref="Capture"/> emits one
    /// entry per quest, so a duplicate is never something a game wrote — it means a file that was
    /// hand-edited or merged, and the order two entries ended up in after a merge says nothing
    /// about which is right. It is also the only rule that cannot lose progress the player really
    /// made, which is what a save exists to hold.
    /// </para>
    /// <para>
    /// This is not the same answer as <see cref="Register"/>, which refuses a duplicate outright,
    /// and the difference is in what the two are reading. <see cref="Register"/> takes a campaign
    /// the build is authoring: two quests under one identifier is a mistake in the build, caught
    /// where it is made. This takes a file the build did not necessarily write: two entries under
    /// one identifier is not two quests, it is one quest described twice, and there is a sensible
    /// reading of that. Refusing here would cost the player everything saved beside it.
    /// </para>
    /// <para>
    /// The rule applies within a single call. A later call is a different save being read, and it
    /// is authoritative — restoring is not a promise that a log can never move backwards.
    /// </para>
    /// </remarks>
    /// <param name="progress">The saved quest states.</param>
    /// <exception cref="ArgumentException">
    /// An entry is <c>null</c>. Checked across the whole batch before any of it is applied, so a
    /// caller that catches this still has the log it started with rather than half a save.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An entry holds a state that is not one a quest has. Refused wherever it appears, including
    /// in the second entry naming a quest — a value outside the lifecycle is not early progress,
    /// so it is never dropped for standing behind something.
    /// </exception>
    public void Restore(IEnumerable<QuestProgress> progress)
    {
        // Materialised because the batch is read twice: once to refuse it whole, once to apply it.
        List<QuestProgress> entries = [.. progress];

        foreach (QuestProgress saved in entries)
        {
            if (saved is null)
            {
                throw new ArgumentException(
                    "The saved progress holds an entry that is not there.", nameof(progress));
            }
        }

        HashSet<string> seen = [];

        foreach (QuestProgress saved in entries)
        {
            // Asked in full rather than left to Find, which takes an identifier a dictionary has to
            // be able to hash: nothing in a running game looks up a quest it cannot name, but a
            // save is not a running game and can hold an entry that names nothing.
            if (string.IsNullOrWhiteSpace(saved.QuestId))
            {
                continue;
            }

            Quest? quest = Find(saved.QuestId);
            if (quest is null)
            {
                continue;
            }

            // the first entry naming a quest is taken as it stands; a second only counts if it
            // carries the quest further than the one already applied
            if (!seen.Add(quest.Id) && saved.State.IsBehind(quest.State))
            {
                continue;
            }

            quest.Restore(saved.State);
        }
    }

    void OnQuestStarted(object? sender, EventArgs e) =>
        QuestStarted?.Invoke(this, new QuestEventArgs((Quest)sender!));

    void OnQuestCompleted(object? sender, EventArgs e) =>
        QuestCompleted?.Invoke(this, new QuestEventArgs((Quest)sender!));
}
