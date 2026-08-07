namespace OliveGameStudio;

/// <summary>
/// A bounded set of slots holding what a character owns, with items of a kind sharing a slot up to
/// their own stack limit.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is slots, never weight.</b> A slot is the only unit and what an item weighs is not
/// modelled, so "have I got room?" is a question the player answers by looking. How many slots
/// there are is a ship stat — <see cref="ShipProfile.CargoSlots"/> — because how much you can carry
/// is the hull's business, while <em>what</em> you own belongs to the character and outlives any
/// hull.
/// </para>
/// <para>
/// <b>Being full is an answer, not a failure.</b> <see cref="Add"/> says no and takes nothing
/// rather than throwing or quietly dropping what would not fit. Carrying capacity is a designed
/// constraint — the player is meant to keep choosing what to keep, sell or leave behind — so
/// running out of room is an ordinary moment in the game, and every caller has somewhere better to
/// put the item than the floor. It is why loot that arrives at a full hold stays in the world
/// rather than evaporating on contact.
/// </para>
/// <para>
/// Slots are held in the order they were opened rather than sorted, so a listing shows the player
/// what they picked up rather than an ordering nothing chose.
/// </para>
/// </remarks>
public sealed class Inventory
{
    readonly List<ItemStack> _stacks = [];

    /// <summary>
    /// Creates an empty inventory of the given size.
    /// </summary>
    /// <param name="slots">How many slots it has.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slots"/> is negative.</exception>
    public Inventory(int slots)
        : this(slots, [])
    {
    }

    /// <summary>
    /// Creates an inventory already holding the given items, used to stock a character from what
    /// content says they start with.
    /// </summary>
    /// <param name="slots">How many slots it has.</param>
    /// <param name="items">The items to start with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slots"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// One of the items is <c>null</c>, or they do not fit. Content stocking a character with more
    /// than the hull can hold is an authoring mistake with no good runtime answer — silently
    /// dropping the overflow starts the player short of what they were written to own, and there is
    /// nowhere else to put it before the game exists — so it is refused where it is authored.
    /// </exception>
    public Inventory(int slots, IEnumerable<Item> items)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slots);
        ArgumentNullException.ThrowIfNull(items);

        Slots = slots;

        // taken one at a time rather than kept, so content handing over a starting inventory does
        // not hand over a list the game then plays with — a template is authored content and never
        // changes
        foreach (Item item in items)
        {
            if (item is null)
            {
                throw new ArgumentException("An inventory cannot hold nothing.", nameof(items));
            }

            if (!Add(item))
            {
                throw new ArgumentException(
                    $"An inventory of {slots} slots cannot hold what it was stocked with.",
                    nameof(items));
            }
        }
    }

    /// <summary>
    /// Gets how many slots there are.
    /// </summary>
    public int Slots { get; }

    /// <summary>
    /// Gets what is in each occupied slot, in the order the slots were opened.
    /// </summary>
    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>
    /// Gets how many slots are occupied.
    /// </summary>
    public int UsedSlots => _stacks.Count;

    /// <summary>
    /// Gets how many slots are empty. A part-filled stack is not one of them, so this is what is
    /// free for something new rather than what is free for anything.
    /// </summary>
    public int FreeSlots => Slots - _stacks.Count;

    /// <summary>
    /// Gets everything held, one entry per item, stacks expanded, in slot order.
    /// </summary>
    /// <remarks>
    /// A listing rather than the storage: it is built on each read, so it is for showing and
    /// asserting on rather than for walking every frame. Anything counting one kind of item asks
    /// <see cref="CountOf"/>, and anything that cares about slots reads <see cref="Stacks"/>.
    /// </remarks>
    public IReadOnlyList<Item> Items =>
        [.. _stacks.SelectMany(stack => Enumerable.Repeat(stack.Item, stack.Count))];

    /// <summary>
    /// Gets how many items are held in total, across every slot.
    /// </summary>
    public int Count => _stacks.Sum(stack => stack.Count);

    /// <summary>
    /// Takes an item in: onto an existing stack of it with room, or into an empty slot.
    /// </summary>
    /// <remarks>
    /// The first stack with room is filled before a slot is opened, so a hold does not fragment
    /// into part-filled slots of the same thing while the player watches it run out of room.
    /// </remarks>
    /// <param name="item">The item to hold.</param>
    /// <returns>
    /// <c>true</c> when it was taken. <c>false</c> when there was no room, in which case nothing
    /// was taken and the caller still has the item.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// An item with this id is already held, authored with different stats. Two authorings of one
    /// id are one item described wrongly rather than two items — <see cref="Item"/> is its id — and
    /// stacking them together would hold one lot of them under the other's stack limit. Caught here
    /// because this is the first place the two are ever seen side by side.
    /// </exception>
    public bool Add(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        ItemStack? held = null;
        ItemStack? withRoom = null;

        foreach (ItemStack stack in _stacks)
        {
            if (!stack.Item.Equals(item))
            {
                continue;
            }

            held ??= stack;

            if (!stack.IsFull)
            {
                withRoom = stack;
                break;
            }
        }

        if (held is not null && !held.Item.Stats.Equals(item.Stats))
        {
            throw new ArgumentException(
                $"'{item.Id}' is already held with different stats; one id is one item.",
                nameof(item));
        }

        if (withRoom is not null)
        {
            withRoom.Add(1);
            return true;
        }

        if (FreeSlots <= 0)
        {
            return false;
        }

        _stacks.Add(new ItemStack(item, count: 1));
        return true;
    }

    /// <summary>
    /// Gives up one of an item.
    /// </summary>
    /// <remarks>
    /// Taken off the newest stack of it rather than the oldest, so the part-filled slot is the one
    /// that empties and a slot comes free as early as it can. Removing from the front instead would
    /// leave a hold of full stacks with a gap at the start and no more room than before.
    /// </remarks>
    /// <param name="item">The item to give up.</param>
    /// <returns><c>true</c> when the item was held and one of it has been removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool Remove(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        for (int slot = _stacks.Count - 1; slot >= 0; slot--)
        {
            ItemStack stack = _stacks[slot];
            if (!stack.Item.Equals(item))
            {
                continue;
            }

            stack.Remove(1);

            if (stack.Count == 0)
            {
                _stacks.RemoveAt(slot);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the item is held.
    /// </summary>
    /// <param name="item">The item to look for.</param>
    /// <returns><c>true</c> when at least one of it is held.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool Contains(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _stacks.Any(stack => stack.Item.Equals(item));
    }

    /// <summary>
    /// How many of an item are held, across every slot holding it.
    /// </summary>
    /// <param name="item">The item to count.</param>
    /// <returns>How many are held, or zero when none are.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public int CountOf(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _stacks.Where(stack => stack.Item.Equals(item)).Sum(stack => stack.Count);
    }

    /// <summary>
    /// Whether there is room for one more of an item — either a stack of it with space, or an empty
    /// slot to open.
    /// </summary>
    /// <remarks>
    /// Asked before something irreversible is done with the item, which is why it exists beside an
    /// <see cref="Add"/> that already reports failure: a drop that has to stay in the world if it
    /// cannot be taken wants the answer without the attempt.
    /// </remarks>
    /// <param name="item">The item to make room for.</param>
    /// <returns><c>true</c> when <see cref="Add"/> would take it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool HasRoomFor(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return FreeSlots > 0 || _stacks.Any(stack => stack.Item.Equals(item) && !stack.IsFull);
    }
}
