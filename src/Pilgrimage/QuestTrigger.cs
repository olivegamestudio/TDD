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
    /// <paramref name="distance"/> is negative or is not a finite number. Both are refused for the
    /// same reason — a trigger that does not describe a place. A negative distance is one nothing
    /// could ever satisfy, so the trigger silently never fires; an infinite one is satisfied by
    /// <em>every</em> position, because the presentation applies this rule as
    /// <c>measured &lt;= Distance</c> and every finite measurement is at most infinity, so the quest
    /// starts or completes the instant the player exists, wherever they are. Neither raises
    /// anything at the moment it goes wrong: the quest log simply reads as though the player had
    /// been everywhere or nowhere, which names no trigger and no frame.
    /// </exception>
    /// <remarks>
    /// There is deliberately no upper bound on a finite distance. The world is unbounded, so a
    /// large radius is a design decision the content is allowed to make; only a value that is not
    /// a distance at all is refused.
    /// </remarks>
    public QuestTrigger(QuestTriggerKind kind, double distance)
    {
        // Ordered before the negative guard so that negative infinity and NaN are named by the rule
        // they actually break. NaN is caught here rather than left to ThrowIfNegative, which stops it
        // only because the sign bit of double.NaN happens to be set — incidental behaviour to lean on
        // for the one value that makes every comparison against it false.
        if (!double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(distance),
                distance,
                "A trigger's distance must be a finite number of world units.");
        }

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
