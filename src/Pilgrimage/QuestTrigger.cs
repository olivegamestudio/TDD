namespace Pilgrimage;

/// <summary>
/// What kind of event fires a quest trigger. The model names the rule; evaluating it belongs to
/// the presentation, which is the layer that knows where things actually are.
/// </summary>
public enum QuestTriggerKind
{
    /// <summary>
    /// The player getting within <see cref="QuestTrigger.Distance"/> of the trigger's marker.
    /// </summary>
    Proximity,
}

/// <summary>
/// The declaration of what begins or finishes a quest: the kind of trigger and how close counts.
/// </summary>
/// <remarks>
/// This is data, not behaviour. Pilgrimage holds no coordinates and measures no distances — the
/// presentation knows where the player and the markers are, applies this rule, and calls
/// <see cref="Quest.Start"/> or <see cref="Quest.Complete"/> when it fires.
/// </remarks>
public sealed record QuestTrigger
{
    /// <summary>
    /// Declares a trigger of the given kind that fires within <paramref name="distance"/>.
    /// </summary>
    /// <param name="kind">What fires the trigger.</param>
    /// <param name="distance">How close the player must get, in world units.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="distance"/> is negative, which nothing could ever satisfy, so the trigger
    /// would silently never fire.
    /// </exception>
    public QuestTrigger(QuestTriggerKind kind, double distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        Kind = kind;
        Distance = distance;
    }

    /// <summary>
    /// Gets what fires the trigger.
    /// </summary>
    public QuestTriggerKind Kind { get; }

    /// <summary>
    /// Gets how close the player must get for the trigger to fire, in world units.
    /// </summary>
    public double Distance { get; }
}
