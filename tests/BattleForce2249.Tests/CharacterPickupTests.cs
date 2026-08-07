using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="Character.Collect"/>: what picking something up does to what the player owns
/// and to what their ship is fitted with, and what a full hold does to both.
/// </summary>
public sealed class CharacterPickupTests
{
    static readonly Item Cannon = new("weapon.cannon", new ItemStats(EquipSlot.Weapon, StackLimit: 1));
    static readonly Item Ore = new("ore.iron", new ItemStats(EquipSlot.None, StackLimit: 20));

    static CharacterTemplate TemplateWith(int cargoSlots) =>
        new(
            "test-character",
            "A test character",
            new ShipProfile(
                DisgracedShip.Handling,
                Health: 100,
                Durability: 100,
                CargoSlots: cargoSlots,
                Loadout: []),
            "test-location",
            []);

    static Character InAShip(int cargoSlots = 8)
    {
        Character character = new(TemplateWith(cargoSlots), new QuestLog());
        character.Board(new Ship(character.Template.Ship));
        return character;
    }

    [Fact]
    public void ACharactersHold_IsAsBigAsTheHullTheyStartIn()
    {
        // how much can be carried is a ship stat; what is carried is theirs
        Assert.Equal(6, InAShip(cargoSlots: 6).Inventory.Slots);
    }

    [Fact]
    public void ACollectedWeapon_IsOwnedAndFittedInOneGo()
    {
        // or the player collects their first weapon and flies on unarmed
        Character character = InAShip();

        Assert.True(character.Collect(Cannon));

        Assert.True(character.Inventory.Contains(Cannon));
        Assert.Equal([Cannon], character.Ship!.Loadout.Weapons);
    }

    [Fact]
    public void FittingSomething_DoesNotTakeItOutOfWhatThePlayerOwns()
    {
        // what the player owns is one list and what is bolted to the hull is a view onto it, so
        // losing the ship loses the fitting and not the item
        Character character = InAShip();

        character.Collect(Cannon);

        Assert.Equal(1, character.Inventory.CountOf(Cannon));
        Assert.Equal(1, character.Inventory.UsedSlots);
    }

    [Fact]
    public void ACollectedItemThatFitsNoSlot_IsSimplyCarried()
    {
        Character character = InAShip();

        Assert.True(character.Collect(Ore));

        Assert.Equal(1, character.Inventory.CountOf(Ore));
        Assert.Equal(0, character.Ship!.Loadout.Count);
    }

    [Fact]
    public void OnceTheWeaponSlotsAreFull_TheNextOneIsCarriedRatherThanFitted()
    {
        Character character = InAShip();

        for (int collected = 0; collected < 5; collected++)
        {
            Assert.True(character.Collect(Cannon));
        }

        Assert.Equal(4, character.Ship!.Loadout.Weapons.Count);
        Assert.Equal(5, character.Inventory.CountOf(Cannon));
    }

    [Fact]
    public void AFullHold_RefusesThePickupEvenWhenTheShipHasASlotFree()
    {
        // equipping is not storage: a rule that let a full hold keep taking weapons as long as the
        // ship had somewhere to bolt them would quietly make the hold's size a lie
        Character character = InAShip(cargoSlots: 1);
        character.Collect(Ore);

        Assert.False(character.Collect(Cannon));

        Assert.False(character.Inventory.Contains(Cannon));
        Assert.Equal(0, character.Ship!.Loadout.Count);
    }

    [Fact]
    public void ACharacterWithNoShipYet_StillOwnsWhatTheyPickUp()
    {
        // a character exists before a hull is built for them, and picking something up must not
        // depend on one
        Character character = new(TemplateWith(cargoSlots: 8), new QuestLog());

        Assert.True(character.Collect(Cannon));

        Assert.True(character.Inventory.Contains(Cannon));
        Assert.Null(character.Ship);
    }

    [Fact]
    public void CollectingNothing_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => InAShip().Collect(null!));
    }
}
