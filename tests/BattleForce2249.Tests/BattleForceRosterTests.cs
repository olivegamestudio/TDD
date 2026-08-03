using System.Globalization;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the characters the game ships: that the Disgraced is the one a new game begins as, that
/// they start where the opening quest is, and that what identifies them is never the text the
/// player reads.
/// </summary>
public sealed class BattleForceRosterTests
{
    readonly BattleForceRoster _roster = new();

    [Fact]
    public void ShipsTheDisgraced()
    {
        CharacterTemplate template = Assert.Single(_roster.Templates);

        Assert.Equal(BattleForceRoster.DisgracedId, template.Id);
    }

    [Fact]
    public void ANewGame_BeginsAsTheDisgraced()
    {
        Assert.Equal(BattleForceRoster.DisgracedId, _roster.Starting.Id);
    }

    [Fact]
    public void TheDisgraced_FliesTheHandlingQuest1WasTunedAgainst()
    {
        // the handling is not a placeholder — it is the tuning quest 1 was checked against — so the
        // character carrying a copy of it rather than the same numbers would be a silent divergence
        Assert.Same(DisgracedShip.Handling, _roster.Starting.Ship.Handling);
    }

    [Fact]
    public void TheDisgraced_StartsInTheMines()
    {
        Assert.Equal(BattleForceWorld.Mines, _roster.Starting.StartLocation);
    }

    [Fact]
    public void TheDisgraced_StartsWithTheHullAndNothingElse()
    {
        // "critically low on everything"
        Assert.Empty(_roster.Starting.StartingInventory);
        Assert.Empty(_roster.Starting.Ship.Loadout);
    }

    [Fact]
    public void TheHullIsFlyableAndCanBeDestroyed()
    {
        // the numbers actually shipped, put through the guards that would refuse them
        Ship ship = new(_roster.Starting.Ship);

        Assert.True(ship.Health.Maximum > 0, "a hull nothing can destroy");
        Assert.True(ship.Durability.Maximum > 0, "a hull that can never wear");
        Assert.True(ship.Handling.MaximumSpeed > 0, "a hull that goes nowhere");
    }

    [Fact]
    public void TheIdentifier_IsTheSameInEveryLanguage()
    {
        // a save written in one language has to load in another, so what identifies the character
        // is never the text the player reads
        using (new CultureScope("ja"))
        {
            Assert.Equal(BattleForceRoster.DisgracedId, _roster.Starting.Id);
        }

        using (new CultureScope("fr"))
        {
            Assert.Equal(BattleForceRoster.DisgracedId, _roster.Starting.Id);
        }
    }

    [Fact]
    public void TheName_IsInThePlayersLanguage()
    {
        using (new CultureScope("en"))
        {
            Assert.Equal("The Disgraced", _roster.Starting.Name);
        }

        using (new CultureScope("fr"))
        {
            Assert.Equal("Le Déchu", _roster.Starting.Name);
        }
    }

    [Fact]
    public void TheRosterIsBuiltPerRead_SoTheLanguageIsNotFrozenAtStartup()
    {
        // the roster is a singleton; a list built once in a field initialiser would hand every
        // later game the language the process happened to start in
        string japanese;
        using (new CultureScope("ja"))
        {
            japanese = _roster.Starting.Name;
        }

        using (new CultureScope("en"))
        {
            Assert.NotEqual(japanese, _roster.Starting.Name);
        }
    }

    [Fact]
    public void TheStartingCharacter_IsOneOfTheOnesTheGameShips()
    {
        Assert.Contains(_roster.Starting.Id, _roster.Templates.Select(template => template.Id));
    }
}
