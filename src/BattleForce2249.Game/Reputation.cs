namespace BattleForce2249;

/// <summary>
/// What each group in the galaxy makes of the character, one standing per group.
/// </summary>
/// <remarks>
/// <para>
/// A group nobody has dealt with stands at zero rather than being absent, so asking about a group
/// the character has never met answers "neutral" rather than nothing. That keeps the caller from
/// having to know which groups exist to ask about one.
/// </para>
/// <para>
/// Groups are named by identifier, never by translated text: standing written to a save in one
/// language has to be found again in another.
/// </para>
/// </remarks>
public sealed class Reputation
{
    readonly Dictionary<string, int> _standings = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets every group the character has a standing with, by identifier. A group at zero that has
    /// been dealt with is here; one that has never been dealt with is not.
    /// </summary>
    public IReadOnlyDictionary<string, int> Standings => _standings;

    /// <summary>
    /// How a group regards the character. Negative is hostile, zero is neutral, positive is
    /// friendly.
    /// </summary>
    /// <param name="group">The group's identifier.</param>
    /// <returns>The standing, or zero for a group the character has never dealt with.</returns>
    /// <exception cref="ArgumentException"><paramref name="group"/> is missing or blank.</exception>
    public int With(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        return _standings.GetValueOrDefault(group);
    }

    /// <summary>
    /// Moves a group's standing.
    /// </summary>
    /// <param name="group">The group's identifier.</param>
    /// <param name="delta">How far to move it. Positive earns favour, negative loses it.</param>
    /// <returns>The standing after the move.</returns>
    /// <exception cref="ArgumentException"><paramref name="group"/> is missing or blank.</exception>
    public int Adjust(string group, int delta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        int standing = With(group) + delta;
        _standings[group] = standing;

        return standing;
    }
}
