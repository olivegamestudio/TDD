namespace OliveGameStudio;

/// <summary>
/// The kind of loadout slot an item fits, if it fits one at all.
/// </summary>
/// <remarks>
/// <para>
/// This is not the item category list — that is a longer list (ammo, material, consumable and the
/// rest) and it is still open. This is the narrower question the loadout actually asks: given an
/// item, which of a ship's slots may it go in? Two items in different categories can answer it the
/// same way, and most items answer <see cref="None"/>.
/// </para>
/// <para>
/// Named for the slot rather than for the item, because the slot is the fixed thing. A ship has
/// four weapon slots whether or not anything in the game is a weapon yet, and an item that fits
/// none of them is carried rather than refused.
/// </para>
/// </remarks>
public enum EquipSlot
{
    /// <summary>
    /// Fits no slot. The item is carried and nothing more — cargo, materials, quest items, and
    /// anything else whose worth is in owning it.
    /// </summary>
    /// <remarks>
    /// Deliberately the default value, so an item nobody thought about is one that is merely
    /// carried rather than one that silently arms a ship.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Fits one of the ship's weapon slots.
    /// </summary>
    Weapon,

    /// <summary>
    /// Fits one of the ship's shield slots.
    /// </summary>
    Shield,

    /// <summary>
    /// Fits one of the ship's additional slots — orbs and the other defensive items that fly
    /// alongside.
    /// </summary>
    Additional,
}
