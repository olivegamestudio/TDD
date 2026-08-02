namespace Pilgrimage;

/// <summary>
/// A quest as the player is living it: an authored <see cref="QuestDefinition"/> plus the state it
/// has reached.
/// </summary>
/// <remarks>
/// The quest is driven entirely through this API. It does not watch the world and does not know
/// what fired its triggers — whatever evaluates them calls <see cref="Start"/> and
/// <see cref="Complete"/>. Both are safe to call repeatedly, because a caller watching proximity
/// has no memory and may report the same arrival on every frame.
/// </remarks>
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
    /// Gets the authored definition this quest was created from, including the triggers whoever
    /// watches the world has to apply.
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
    /// Occurs the first time the quest begins, once per quest rather than once per call.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Occurs the first time the quest completes, once per quest rather than once per call.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Begins the quest. A quest that has already started or completed is left alone.
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
    /// Completes the quest. Only an active quest can be completed: one that never started has not
    /// been played, and one already finished cannot finish twice.
    /// </summary>
    public void Complete()
    {
        if (State is not QuestState.Active)
        {
            return;
        }

        State = QuestState.Completed;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Puts the quest straight into a state read back from a save game, without raising
    /// <see cref="Started"/> or <see cref="Completed"/> — the player already lived those moments.
    /// </summary>
    /// <remarks>
    /// This takes the state it is given, forwards or back; it is not the place that decides which
    /// of several saved states a quest should come back in. A save naming one quest more than once
    /// is resolved by <see cref="QuestLog.Restore"/>, which calls this only for a state that
    /// carries the quest further than the one already applied.
    /// </remarks>
    /// <param name="state">The state the quest was saved in.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> is not one of the states a quest has. A quest holding a state that
    /// does not exist can never be started and can never be completed, and nothing downstream would
    /// report it — so a restore that would brick the quest is refused where it happens instead.
    /// </exception>
    public void Restore(QuestState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state), state, $"'{(int)state}' is not a quest state.");
        }

        State = state;
    }
}
