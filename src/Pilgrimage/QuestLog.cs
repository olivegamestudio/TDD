namespace Pilgrimage;

/// <summary>
/// The player's quests: every quest the campaign registered, and the state each has reached. It
/// forwards a frame's player position to all of them and republishes their events, so anything
/// that cares about quest progress — saving, the eventual quest display — subscribes here rather
/// than to each quest.
/// </summary>
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
    /// Advances every registered quest for the player's current position. Called once per frame.
    /// </summary>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(Position playerPosition)
    {
        // ToList: a quest event handler may register or clear quests
        foreach (Quest quest in _quests.Values.ToList())
        {
            quest.Update(playerPosition);
        }
    }

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

    void OnQuestStarted(object? sender, EventArgs e) =>
        QuestStarted?.Invoke(this, new QuestEventArgs((Quest)sender!));

    void OnQuestCompleted(object? sender, EventArgs e) =>
        QuestCompleted?.Invoke(this, new QuestEventArgs((Quest)sender!));
}
