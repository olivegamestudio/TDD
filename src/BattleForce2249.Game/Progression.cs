namespace BattleForce2249;

/// <summary>
/// How far a character has come: what they have earned, what rank that has bought them, what they
/// have left to spend, and the gifts they have been granted along the way.
/// </summary>
/// <remarks>
/// <para>
/// This is part of the persistent record pillar 4 protects. It belongs to the character rather than
/// to the ship, so it survives losing the ship — death costs position, not progress.
/// </para>
/// <para>
/// <b>No curve is stated here.</b> How much experience buys a level, and how many points a level is
/// worth, are content decisions nobody has made; a curve invented here would be a number the design
/// then has to argue with. So experience accumulates and <see cref="Advance"/> is called by whatever
/// eventually owns that rule.
/// </para>
/// </remarks>
public sealed class Progression
{
    readonly HashSet<string> _gifts = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the experience earned so far.
    /// </summary>
    public int Experience { get; private set; }

    /// <summary>
    /// Gets the level reached. A character begins at level one rather than zero: the first level is
    /// something they are, not something they have yet to earn.
    /// </summary>
    public int Level { get; private set; } = 1;

    /// <summary>
    /// Gets the points earned but not yet spent.
    /// </summary>
    public int SpendPoints { get; private set; }

    /// <summary>
    /// Gets the gifts the character has been granted, by identifier. Identifiers are never
    /// translated; what a gift is called in the player's language is looked up from this.
    /// </summary>
    public IReadOnlyCollection<string> Gifts => _gifts;

    /// <summary>
    /// Earns experience.
    /// </summary>
    /// <param name="experience">How much to earn. Zero is allowed and does nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative. Experience is not taken back — a system that means to penalise the
    /// player should say so rather than reach through here, because the two need telling apart the
    /// moment anything reports what was earned.
    /// </exception>
    public void Gain(int experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);

        Experience += experience;
    }

    /// <summary>
    /// Takes the character up a level and hands them the points it is worth.
    /// </summary>
    /// <param name="spendPoints">How many points the level grants.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spendPoints"/> is negative.</exception>
    public void Advance(int spendPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spendPoints);

        Level++;
        SpendPoints += spendPoints;
    }

    /// <summary>
    /// Spends points the character has earned.
    /// </summary>
    /// <param name="points">How many to spend.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative, or more than the character holds. Overspending would leave a
    /// character owing points, which is not a state anything else knows how to read.
    /// </exception>
    public void Spend(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(points, SpendPoints);

        SpendPoints -= points;
    }

    /// <summary>
    /// Grants a gift, which a character either has or does not — granting one twice is not twice
    /// the gift.
    /// </summary>
    /// <param name="giftId">The gift's identifier.</param>
    /// <returns><c>true</c> when the character did not already have it.</returns>
    /// <exception cref="ArgumentException"><paramref name="giftId"/> is missing or blank.</exception>
    public bool Grant(string giftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(giftId);

        return _gifts.Add(giftId);
    }

    /// <summary>
    /// Whether the character has been granted a gift.
    /// </summary>
    /// <param name="giftId">The gift's identifier.</param>
    /// <returns><c>true</c> when they have it.</returns>
    /// <exception cref="ArgumentException"><paramref name="giftId"/> is missing or blank.</exception>
    public bool Has(string giftId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(giftId);

        return _gifts.Contains(giftId);
    }
}
