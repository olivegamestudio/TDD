namespace OliveGameStudio;

/// <summary>
/// A set of items held together: what a character owns, or what a ship is currently fitted with.
/// </summary>
/// <remarks>
/// <para>
/// One type serves both because at this stage they are the same question — which items are here —
/// and the difference between owning an item and having it equipped is which inventory it is in.
/// Equip slots are what will separate them, and the slot model is an open decision.
/// </para>
/// <para>
/// Items are held in the order they were added rather than sorted, so a listing shows the player
/// what they picked up rather than an ordering nothing chose.
/// </para>
/// </remarks>
public sealed class Inventory
{
    readonly List<Item> _items;

    /// <summary>
    /// Creates an empty inventory.
    /// </summary>
    public Inventory()
        : this([])
    {
    }

    /// <summary>
    /// Creates an inventory already holding the given items, used to stock a character or fit a
    /// ship from what content says it starts with.
    /// </summary>
    /// <param name="items">The items to start with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public Inventory(IEnumerable<Item> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // copied rather than kept, so content handing over its starting loadout does not hand over
        // a list the game then plays with — a template is authored content and never changes
        _items = [.. items];

        if (_items.Any(item => item is null))
        {
            throw new ArgumentException("An inventory cannot hold nothing.", nameof(items));
        }
    }

    /// <summary>
    /// Gets what is held, in the order it arrived.
    /// </summary>
    public IReadOnlyList<Item> Items => _items;

    /// <summary>
    /// Gets how many items are held.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Takes an item in.
    /// </summary>
    /// <param name="item">The item to hold.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public void Add(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);
    }

    /// <summary>
    /// Gives up one of an item.
    /// </summary>
    /// <param name="item">The item to give up.</param>
    /// <returns><c>true</c> when the item was held and one of it has been removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool Remove(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _items.Remove(item);
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

        return _items.Contains(item);
    }
}
