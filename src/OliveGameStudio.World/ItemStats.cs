namespace OliveGameStudio;

/// <summary>
/// What an item is worth to an inventory and to a loadout: how high it stacks, and which slot — if
/// any — it may be fitted into.
/// </summary>
/// <remarks>
/// <para>
/// The stats type for an item, in the sense the rest of the model already uses one:
/// <see cref="ShipHandling"/> for a hull, <see cref="ShieldStats"/> for a shield,
/// <see cref="OrbStats"/> for an orb. Authored alongside the item, never changed by play.
/// </para>
/// <para>
/// It carries what <em>holding and fitting</em> an item needs and nothing about what the item
/// does. A shield's absorption is <see cref="ShieldStats"/> and an orb's orbit is
/// <see cref="OrbStats"/>; a weapon has no stats type at all yet, because nothing fires one. Those
/// numbers belong to the systems that read them, and folding them in here would make one type the
/// place every future system has to be added to.
/// </para>
/// </remarks>
/// <param name="Slot">
/// Which of a ship's loadout slots this item fits. <see cref="EquipSlot.None"/> — the default —
/// means it is carried and never fitted.
/// </param>
/// <param name="StackLimit">
/// How many of the item share one inventory slot. One means a slot each.
/// </param>
public sealed record ItemStats(EquipSlot Slot, int StackLimit)
{
    /// <summary>
    /// The most of any one item that a single inventory slot holds.
    /// </summary>
    /// <remarks>
    /// A stated ceiling rather than a number each item type picks freely. Carrying capacity is a
    /// designed constraint — the player is meant to keep choosing what to keep, sell or leave
    /// behind — and a stack limit is a hole straight through it: one item type authored to stack to
    /// a thousand would carry more in one slot than the whole ship is meant to hold. Ninety-nine is
    /// the top of the range the design names, and it is deliberately low enough that a full hold
    /// still runs out.
    /// </remarks>
    public const int MaxStackLimit = 99;

    /// <summary>
    /// Something carried one to a slot and fitted to nothing: the plainest item there is, and what
    /// an item whose stats nobody has thought about should be.
    /// </summary>
    public static ItemStats Single { get; } = new(EquipSlot.None, StackLimit: 1);

    /// <summary>
    /// Which of a ship's loadout slots this item fits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The slot is not one of the slots a ship has. A loadout matches an item to a slot by this
    /// value, so an item naming a slot that does not exist could never be fitted and nothing
    /// downstream would say why — it would simply refuse to equip, for ever, looking like a full
    /// ship.
    /// </exception>
    public EquipSlot Slot { get; } = Validated(Slot);

    /// <summary>
    /// How many of the item share one inventory slot.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The limit is below one or above <see cref="MaxStackLimit"/>. A limit of zero is an item that
    /// can be owned and never held — every add opens a slot that immediately has no room in it —
    /// and a negative one is the same defect with the sign lost. Above the ceiling is refused for
    /// the reason the ceiling exists.
    /// </exception>
    public int StackLimit { get; } = Validated(StackLimit);

    static EquipSlot Validated(EquipSlot slot)
    {
        if (!Enum.IsDefined(slot))
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                slot,
                "An item can only fit a slot a ship has.");
        }

        return slot;
    }

    static int Validated(int stackLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stackLimit, 1, nameof(stackLimit));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stackLimit, MaxStackLimit, nameof(stackLimit));

        return stackLimit;
    }
}
