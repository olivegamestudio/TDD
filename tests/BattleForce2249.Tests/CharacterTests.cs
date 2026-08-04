using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="Character"/>: what the player has, what they are currently sitting in, and
/// which of the two survives losing the other.
/// </summary>
public sealed class CharacterTests
{
    static readonly Item Cell = new("power-cell");

    static CharacterTemplate TemplateWith(IReadOnlyList<Item>? inventory = null) =>
        new(
            "test-character",
            "A test character",
            new ShipProfile(DisgracedShip.Handling, Health: 100, Durability: 100, Loadout: []),
            "test-location",
            inventory ?? []);

    static Character CharacterWith(IReadOnlyList<Item>? inventory = null) =>
        new(TemplateWith(inventory), new QuestLog());

    [Fact]
    public void ANewCharacter_IsAtTheStartOfTheirStory()
    {
        Character character = CharacterWith();

        Assert.Equal(0, character.Credits);
        Assert.Equal(1, character.Progression.Level);
        Assert.Empty(character.Reputation.Standings);
        Assert.Empty(character.Quests.Quests);
    }

    [Fact]
    public void ANewCharacter_OwnsWhatTheirTemplateSaysTheyStartWith()
    {
        Character character = CharacterWith([Cell]);

        Assert.Equal([Cell], character.Inventory.Items);
    }

    [Fact]
    public void ACharactersInventory_IsTheirsRatherThanTheTemplates()
    {
        // a template is authored content and never changes at runtime, so picking something up must
        // not change what the next character starts with
        CharacterTemplate template = TemplateWith([Cell]);
        Character character = new(template, new QuestLog());

        character.Inventory.Add(new Item("scrap"));

        Assert.Equal([Cell], template.StartingInventory);
    }

    [Fact]
    public void ACharacter_RecordsTheirStoryInTheLogTheyWereGiven()
    {
        // passed in rather than made here, because whoever subscribes to it outlives any one
        // character: a new game replaces the character, and a log replaced with it would take every
        // subscriber's wiring with it
        QuestLog quests = new();
        Character character = new(TemplateWith(), quests);

        Assert.Same(quests, character.Quests);
    }

    [Fact]
    public void ANewCharacter_IsNotYetInAShip()
    {
        // a character exists before a hull is built for them, and losing a hull has to be
        // expressible without losing the character
        Character character = CharacterWith();

        Assert.Null(character.Ship);
    }

    [Fact]
    public void Board_PutsTheCharacterIntoAShip()
    {
        Character character = CharacterWith();
        Ship ship = new(character.Template.Ship);

        character.Board(ship);

        Assert.Same(ship, character.Ship);
    }

    [Fact]
    public void ChangingShip_KeepsEverythingTheCharacterHasEarned()
    {
        // pillar 4 as a property rather than a paragraph: the persistent record survives, and a
        // hull is not part of it
        Character character = CharacterWith();
        character.Earn(500);
        character.Progression.Gain(120);
        character.Reputation.Adjust("miners", 10);
        character.Board(new Ship(character.Template.Ship));

        character.Board(new Ship(character.Template.Ship));

        Assert.Equal(500, character.Credits);
        Assert.Equal(120, character.Progression.Experience);
        Assert.Equal(10, character.Reputation.With("miners"));
    }

    [Fact]
    public void Earn_AddsToWhatTheCharacterHas()
    {
        Character character = CharacterWith();

        character.Earn(250);
        character.Earn(50);

        Assert.Equal(300, character.Credits);
    }

    [Fact]
    public void Spend_TakesFromWhatTheCharacterHas()
    {
        Character character = CharacterWith();
        character.Earn(300);

        character.Spend(120);

        Assert.Equal(180, character.Credits);
    }

    [Fact]
    public void SpendingMoreThanTheCharacterHas_IsRefused()
    {
        // debt is not a state anything else knows how to read, so a purchase that cannot be
        // afforded fails where it is attempted rather than leaving a negative balance behind
        Character character = CharacterWith();
        character.Earn(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => character.Spend(101));
        Assert.Equal(100, character.Credits);
    }

    [Fact]
    public void Earning_CannotPutTheCharacterIntoDebtThroughOverflow()
    {
        // debt is the state Spend exists to prevent, and a wrap past int.MaxValue creates it
        // silently — no exception where it happens, and nothing to spend ever again, because Spend
        // reads the negative balance as the ceiling
        Character character = CharacterWith();
        character.Earn(int.MaxValue);

        character.Earn(1);

        Assert.Equal(int.MaxValue, character.Credits);
    }

    [Fact]
    public void OneEarnPastTheTop_IsHeldAtTheTopRatherThanWrapping()
    {
        // the whole earn is clamped, not the last of it: a single payment larger than the room left
        // is the same failure as two that add up to more than it
        Character character = CharacterWith();
        character.Earn(1);

        character.Earn(int.MaxValue);

        Assert.Equal(int.MaxValue, character.Credits);
    }

    [Fact]
    public void CreditsHeldAtTheTop_CanStillBeSpent()
    {
        // holding at the ceiling is a ceiling, not a lock: the balance the issue reports as
        // permanently unspendable spends normally
        Character character = CharacterWith();
        character.Earn(int.MaxValue);
        character.Earn(1);

        character.Spend(120);

        Assert.Equal(int.MaxValue - 120, character.Credits);
    }

    [Fact]
    public void EarningOrSpendingANegativeAmount_IsRefused()
    {
        Character character = CharacterWith();

        Assert.Throws<ArgumentOutOfRangeException>(() => character.Earn(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => character.Spend(-1));
        Assert.Equal(0, character.Credits);
    }

    [Fact]
    public void ACharacterWithNoTemplateOrNoLog_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Character(null!, new QuestLog()));
        Assert.Throws<ArgumentNullException>(() => new Character(TemplateWith(), null!));
        Assert.Throws<ArgumentNullException>(() => CharacterWith().Board(null!));
    }
}
