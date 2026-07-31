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
    /// A quest with the same identifier is already registered. Two quests sharing an identifier
    /// would overwrite each other in the save game.
    /// </exception>
    public Quest Register(QuestDefinition definition)
    {
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
    /// <returns>One entry per registered quest.</returns>
    public IReadOnlyList<QuestProgress> Capture() =>
        [.. _quests.Values.Select(quest => new QuestProgress(quest.Id, quest.State))];

    /// <summary>
    /// Puts the registered quests back into their saved states, raising no events.
    /// </summary>
    /// <remarks>
    /// Saves and campaigns drift apart over a game's life, and both directions are tolerated: a
    /// quest the save knows about but this build no longer ships is ignored, and a quest added
    /// since the save was written simply starts from the beginning.
    /// </remarks>
    /// <param name="progress">The saved quest states.</param>
    public void Restore(IEnumerable<QuestProgress> progress)
    {
        foreach (QuestProgress saved in progress)
        {
            Find(saved.QuestId)?.Restore(saved.State);
        }
    }

    void OnQuestStarted(object? sender, EventArgs e) =>
        QuestStarted?.Invoke(this, new QuestEventArgs((Quest)sender!));

    void OnQuestCompleted(object? sender, EventArgs e) =>
        QuestCompleted?.Invoke(this, new QuestEventArgs((Quest)sender!));
}
