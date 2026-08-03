namespace OliveGameStudio;

/// <summary>
/// A pool and how much of it is left: what something was built or equipped to hold, and what it
/// holds now.
/// </summary>
/// <remarks>
/// <para>
/// One type rather than three near-identical pairs of numbers. A hull's health, a shield and a
/// hull's durability differ only in what empties them and what fills them again; written out three
/// times, the clamping is written three times and only has to be got wrong once.
/// </para>
/// <para>
/// What happens when a meter reaches zero is deliberately not decided here. Emptying is a rule
/// about the thing that owns the meter — a ship at zero health and an item at zero durability are
/// not the same event — and the item half of that question is still open.
/// </para>
/// </remarks>
public sealed class Meter
{
    /// <summary>
    /// Creates a meter that starts full.
    /// </summary>
    /// <param name="maximum">
    /// How much the pool holds. Zero is allowed and means the thing has none of whatever this
    /// measures — a ship with no shield fitted rather than a ship whose shield is down.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The maximum is negative, not a number, or infinite. A negative pool is empty before anything
    /// touches it, and an infinite one can never be emptied — both are content mistakes that would
    /// otherwise only show up as a ship that cannot be destroyed or one that arrives destroyed.
    /// </exception>
    public Meter(double maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        ArgumentOutOfRangeException.ThrowIfEqual(maximum, double.PositiveInfinity);

        Maximum = maximum;
        Current = maximum;
    }

    /// <summary>
    /// Gets how much the pool holds when it is full.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets how much is left.
    /// </summary>
    public double Current { get; private set; }

    /// <summary>
    /// Gets a value indicating whether nothing is left.
    /// </summary>
    public bool IsEmpty => Current <= 0;

    /// <summary>
    /// Takes from the pool, stopping at empty.
    /// </summary>
    /// <param name="amount">How much to take. Zero is allowed and does nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative or not a number. Taking a negative amount is restoring, and a caller
    /// that means to restore should say so — reading it as one would let a damage calculation that
    /// came out backwards heal the thing it was aimed at.
    /// </exception>
    public void Reduce(double amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        Current = Math.Max(0, Current - amount);
    }

    /// <summary>
    /// Puts back into the pool, stopping at full.
    /// </summary>
    /// <param name="amount">How much to put back. Zero is allowed and does nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative or not a number, for the same reason <see cref="Reduce"/> refuses one.
    /// </exception>
    public void Restore(double amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        Current = Math.Min(Maximum, Current + amount);
    }
}
