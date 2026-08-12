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
    /// Moves a group's standing, holding it at the end of the range rather than letting it wrap
    /// past.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A move that would carry the standing past <see cref="int.MaxValue"/> or
    /// <see cref="int.MinValue"/> stops there. Left to wrap, earning favour with a group already at
    /// the top would come back as <see cref="int.MinValue"/> — a whole game's standing reversed to
    /// maximally hostile by one award, raising nothing and naming nothing. The sign carries the
    /// relationship, so that is not a number being slightly wrong; it is the wrong faction.
    /// </para>
    /// <para>
    /// Clamped rather than refused, because the caller has not made a mistake: a reward of the
    /// usual size applied to a standing that happens to be at the end of the range is an ordinary
    /// call, and throwing would take the game down for a quest the player just succeeded at. Saying
    /// "no further" costs the difference between two standings that are both already as friendly as
    /// the model can express, and keeps the guarantee that matters — a positive delta can never
    /// leave a group less friendly than it found them, and a negative one can never leave them more.
    /// </para>
    /// <para>
    /// A move of nothing against a group the character has never dealt with leaves no trace, rather
    /// than listing them at zero. The two zeroes mean different things — one is a stranger, the
    /// other a group they fell out with and made it up to — and only <see cref="Standings"/> can
    /// tell them apart. Writing the stranger in would spend that distinction on a call that changed
    /// nothing, and a save written afterwards would carry the invention forward for good. A group
    /// already listed stays listed: a zero move does not undo the history that put them there.
    /// </para>
    /// </remarks>
    /// <param name="group">The group's identifier.</param>
    /// <param name="delta">How far to move it. Positive earns favour, negative loses it.</param>
    /// <returns>The standing after the move.</returns>
    /// <exception cref="ArgumentException"><paramref name="group"/> is missing or blank.</exception>
    public int Adjust(string group, int delta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        if (delta == 0 && !_standings.ContainsKey(group))
        {
            return 0;
        }

        // Widened before the addition rather than after it: the sum of two int values always fits
        // in a long, so there is no wrap to detect afterwards — by then the evidence is gone.
        long moved = (long)With(group) + delta;
        int standing = (int)Math.Clamp(moved, int.MinValue, int.MaxValue);
        _standings[group] = standing;

        return standing;
    }
}
