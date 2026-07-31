namespace Pilgrimage;

/// <summary>
/// A quest as the player is living it: an authored <see cref="QuestDefinition"/> plus the state it
/// has reached. The quest is driven by where the player is, so a frame that moves the player onto
/// a marker is all it takes to start or finish one.
/// </summary>
/// <param name="definition">The authored quest this instance tracks.</param>
public sealed class Quest(QuestDefinition definition)
{
    /// <summary>
    /// Gets the stable identifier this quest is saved under.
    /// </summary>
    public string Id => definition.Id;

    /// <summary>
    /// Gets the quest title as shown to the player.
    /// </summary>
    public string Title => definition.Title;

    /// <summary>
    /// Gets the authored definition this quest was created from.
    /// </summary>
    public QuestDefinition Definition => definition;

    /// <summary>
    /// Gets the point the quest has reached in its lifecycle.
    /// </summary>
    public QuestState State { get; private set; } = QuestState.NotStarted;

    /// <summary>
    /// Gets a value indicating whether the quest has begun and is not yet finished.
    /// </summary>
    public bool IsActive => State is QuestState.Active;

    /// <summary>
    /// Gets a value indicating whether the quest has been completed.
    /// </summary>
    public bool IsCompleted => State is QuestState.Completed;

    /// <summary>
    /// Occurs the first time the quest begins. It is raised once per quest, not once per frame the
    /// player spends on the start marker.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Occurs the first time the quest completes. It is raised once per quest, not once per frame
    /// the player spends on the end marker.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Begins the quest. A quest that has already started or completed is left alone, so this is
    /// safe to call from anywhere that thinks the quest should be running.
    /// </summary>
    public void Start()
    {
        if (State is not QuestState.NotStarted)
        {
            return;
        }

        State = QuestState.Active;
        Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Advances the quest for the player's current position, starting it when the start marker is
    /// reached and completing it when the end marker is. Called once per frame.
    /// </summary>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(Position playerPosition)
    {
        if (State is QuestState.NotStarted && definition.AutoStarts && definition.Start.IsReachedBy(playerPosition))
        {
            Start();
        }

        if (State is QuestState.Active && definition.End.IsReachedBy(playerPosition))
        {
            Complete();
        }
    }

    /// <summary>
    /// Puts the quest straight into a state read back from a save game, without raising
    /// <see cref="Started"/> or <see cref="Completed"/> — the player already lived those moments.
    /// </summary>
    /// <param name="state">The state the quest was saved in.</param>
    public void Restore(QuestState state) => State = state;

    /// <summary>
    /// Completes the quest and raises <see cref="Completed"/>.
    /// </summary>
    void Complete()
    {
        State = QuestState.Completed;
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
