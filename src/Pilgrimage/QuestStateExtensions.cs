namespace Pilgrimage;

/// <summary>
/// Treats <see cref="QuestState"/> as the order a quest passes through rather than as a set of
/// values.
/// </summary>
/// <remarks>
/// A quest's lifecycle only ever runs one way — <see cref="Quest.Start"/> and
/// <see cref="Quest.Complete"/> are both one-way and both safe to call repeatedly — but nothing
/// said so anywhere a caller could ask. This is where that is said, so a rule about which of two
/// states represents more progress is a decision written down rather than an enum's declaration
/// order relied on by accident.
/// </remarks>
public static class QuestStateExtensions
{
    /// <summary>
    /// Determines whether <paramref name="state"/> is earlier in the quest lifecycle than
    /// <paramref name="other"/> — that is, whether taking it instead would hand progress back.
    /// </summary>
    /// <param name="state">The state to test.</param>
    /// <param name="other">The state to test it against.</param>
    /// <returns>
    /// <c>true</c> when <paramref name="state"/> is strictly earlier than <paramref name="other"/>.
    /// Equal states are behind nothing: two statements that agree disagree about no progress.
    /// A value that is not one of the states a quest has is also behind nothing — it is not a point
    /// on the lifecycle at all, so calling it "early" would let a caller quietly drop it. Refusing
    /// it belongs to <see cref="Quest.Restore"/>, which is the one place that can say why.
    /// </returns>
    public static bool IsBehind(this QuestState state, QuestState other) =>
        Enum.IsDefined(state) && Enum.IsDefined(other) && state < other;
}
