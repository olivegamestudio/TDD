namespace OliveGameStudio;

/// <summary>
/// The ship's equip slots and what is fitted in them: four weapons, two shields and two additional
/// slots, all empty until something is put in them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The layout is fixed for now, and deliberately.</b> Every hull has the same eight slots, so a
/// loadout is a thing the player fills rather than a thing the ship they happen to be flying
/// decides for them. Letting slot counts vary by hull is a later decision; when it arrives, these
/// counts become numbers the hull supplies and this type takes them — which is the same shape
/// <see cref="Shielding.Slots"/> and <see cref="Orbs.Slots"/> are already waiting in, and why the
/// counts for those two are read from them here rather than written down twice.
/// </para>
/// <para>
/// <b>This is what is fitted; it is not yet what that is worth.</b> An item names the slot it fits
/// and nothing more, so a shield item sitting in a shield slot does not by itself give the ship a
/// shield layer — <see cref="Ship.Shielding"/> still carries the numbers, and joining the two is
/// the step after this one. Until then a loadout is the record of what the player has equipped, and
/// it is the record the drag-and-drop screen will be built on.
/// </para>
/// <para>
/// It knows nothing about ownership. Fitting an item the character does not own is not a mistake it
/// can see — a loadout holds slots, and what the player owns is the character's
/// <see cref="Inventory"/>. Pickup goes through the character for exactly that reason.
/// </para>
/// </remarks>
public sealed class Loadout
{
    /// <summary>
    /// How many weapons a ship carries.
    /// </summary>
    /// <remarks>
    /// Four, against two of everything else, because weapons are the choice the player makes most
    /// often and the one that most decides how a fight goes.
    /// </remarks>
    public const int WeaponSlots = 4;

    readonly List<Item> _weapons = [];
    readonly List<Item> _shields = [];
    readonly List<Item> _additional = [];

    /// <summary>
    /// Gets what is in the weapon slots, in the order it was fitted.
    /// </summary>
    public IReadOnlyList<Item> Weapons => _weapons;

    /// <summary>
    /// Gets what is in the shield slots, in the order it was fitted.
    /// </summary>
    public IReadOnlyList<Item> Shields => _shields;

    /// <summary>
    /// Gets what is in the additional slots — orbs and the other defensive items — in the order it
    /// was fitted.
    /// </summary>
    public IReadOnlyList<Item> Additional => _additional;

    /// <summary>
    /// Gets how many items are fitted across every slot.
    /// </summary>
    public int Count => _weapons.Count + _shields.Count + _additional.Count;

    /// <summary>
    /// How many slots of a kind a ship has.
    /// </summary>
    /// <param name="slot">The kind of slot.</param>
    /// <returns>
    /// How many there are. <see cref="EquipSlot.None"/> has none, because it is what an item that
    /// fits nothing says rather than a slot on the ship.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The slot is not one a ship has.
    /// </exception>
    public static int SlotsFor(EquipSlot slot) => slot switch
    {
        EquipSlot.None => 0,
        EquipSlot.Weapon => WeaponSlots,
        EquipSlot.Shield => Shielding.Slots,
        EquipSlot.Additional => Orbs.Slots,
        _ => throw new ArgumentOutOfRangeException(
            nameof(slot),
            slot,
            "A ship has no slots of that kind."),
    };

    /// <summary>
    /// What is fitted in the slots of a kind.
    /// </summary>
    /// <param name="slot">The kind of slot.</param>
    /// <returns>What is in them, in the order it was fitted; empty for <see cref="EquipSlot.None"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The slot is not one a ship has.
    /// </exception>
    public IReadOnlyList<Item> In(EquipSlot slot) => Fitted(slot) ?? [];

    /// <summary>
    /// The slots of a kind, as the list they are actually kept in, or <c>null</c> for
    /// <see cref="EquipSlot.None"/> — which is not a slot on the ship and so has no list.
    /// </summary>
    List<Item>? Fitted(EquipSlot slot) => slot switch
    {
        EquipSlot.None => null,
        EquipSlot.Weapon => _weapons,
        EquipSlot.Shield => _shields,
        EquipSlot.Additional => _additional,
        _ => throw new ArgumentOutOfRangeException(
            nameof(slot),
            slot,
            "A ship has no slots of that kind."),
    };

    /// <summary>
    /// How many slots of a kind are empty.
    /// </summary>
    /// <param name="slot">The kind of slot.</param>
    /// <returns>How many are free.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The slot is not one a ship has.
    /// </exception>
    public int FreeIn(EquipSlot slot) => SlotsFor(slot) - In(slot).Count;

    /// <summary>
    /// Fits an item into a free slot of its kind — the auto-slot rule a collected item goes through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Picking something up should arm the ship with it when there is nowhere else it would rather
    /// go, because the alternative is a player who collects their first weapon and flies on
    /// unarmed, having been given no reason to think a screen exists that they have to open. When
    /// the slots are full the item is simply carried, and moving it in is the player's decision to
    /// make deliberately.
    /// </para>
    /// <para>
    /// Which free slot it lands in is not a decision: they are identical, so it takes the next one.
    /// </para>
    /// </remarks>
    /// <param name="item">The item to fit.</param>
    /// <returns>
    /// <c>true</c> when it was fitted. <c>false</c> when it fits no slot at all, or every slot of
    /// its kind is taken — neither of which is a failure, and in both the loadout is unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool TryEquip(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        EquipSlot slot = item.Stats.Slot;

        if (Fitted(slot) is not { } fitted || FreeIn(slot) <= 0)
        {
            return false;
        }

        fitted.Add(item);
        return true;
    }

    /// <summary>
    /// Takes an item back out of the slot it is fitted in.
    /// </summary>
    /// <remarks>
    /// The last one fitted comes out first when several of the same item are in the slots, which
    /// leaves the earlier ones where the player put them.
    /// </remarks>
    /// <param name="item">The item to take out.</param>
    /// <returns><c>true</c> when one of it was fitted and has been taken out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public bool Unequip(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Fitted(item.Stats.Slot) is not { } fitted)
        {
            return false;
        }

        int last = fitted.LastIndexOf(item);
        if (last < 0)
        {
            return false;
        }

        fitted.RemoveAt(last);
        return true;
    }
}
