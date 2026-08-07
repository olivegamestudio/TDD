namespace OliveGameStudio;

/// <summary>
/// One inventory slot: an item, and how many of it are in that slot.
/// </summary>
/// <remarks>
/// A slot rather than a total. Two stacks of the same item are two slots, which is the whole point
/// of a stack limit — an item that stacks to ten takes a second slot on the eleventh, and the
/// player feels the hold filling up. Anything wanting the total asks
/// <see cref="Inventory.CountOf"/>.
/// </remarks>
public sealed class ItemStack
{
    internal ItemStack(Item item, int count)
    {
        Item = item;
        Count = count;
    }

    /// <summary>
    /// Gets what is in the slot.
    /// </summary>
    public Item Item { get; }

    /// <summary>
    /// Gets how many of it are in the slot. Never zero: a slot that empties is given up rather than
    /// held on to, or a hold could fill with nothing.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets how many more of the item this slot will take before another slot is needed.
    /// </summary>
    public int Room => Item.Stats.StackLimit - Count;

    /// <summary>
    /// Gets whether this slot will take no more.
    /// </summary>
    public bool IsFull => Room <= 0;

    internal void Add(int amount) => Count += amount;

    internal void Remove(int amount) => Count -= amount;
}
