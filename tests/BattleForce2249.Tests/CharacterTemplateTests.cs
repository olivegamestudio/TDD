using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers <see cref="CharacterTemplate"/> as authored content: what it refuses, and why it refuses
/// it where the author wrote it rather than where the game later trips over it.
/// </summary>
public sealed class CharacterTemplateTests
{
    static ShipProfile Ship =>
        new(DisgracedShip.Handling, Health: 100, Durability: 100, CargoSlots: 16, Loadout: [], HullRadius: 1);

    [Fact]
    public void AnAuthoredTemplate_KeepsWhatItWasGiven()
    {
        Item cell = new("power-cell", ItemStats.Single);
        ShipProfile hull = Ship;

        CharacterTemplate template = new("id", "A name", hull, "start", [cell]);

        Assert.Equal("id", template.Id);
        Assert.Equal("A name", template.Name);
        Assert.Same(hull, template.Ship);
        Assert.Equal("start", template.StartLocation);
        Assert.Equal([cell], template.StartingInventory);
    }

    [Fact]
    public void ATemplateWithNoIdentifier_IsRefusedWhereItIsAuthored()
    {
        ArgumentNullException refused = Assert.Throws<ArgumentNullException>(
            () => new CharacterTemplate(null!, "A name", Ship, "start", []));

        Assert.Equal(nameof(CharacterTemplate.Id), refused.ParamName);
    }

    [Fact]
    public void ATemplateWithNoName_IsRefusedWhereItIsAuthored()
    {
        ArgumentNullException refused = Assert.Throws<ArgumentNullException>(
            () => new CharacterTemplate("id", null!, Ship, "start", []));

        Assert.Equal(nameof(CharacterTemplate.Name), refused.ParamName);
    }

    [Fact]
    public void ATemplateWithNoHull_IsRefusedWhereItIsAuthored()
    {
        // the failure this replaces was a NullReferenceException from Character's constructor,
        // naming nothing the author had written
        ArgumentNullException refused = Assert.Throws<ArgumentNullException>(
            () => new CharacterTemplate("id", "A name", null!, "start", []));

        Assert.Equal(nameof(CharacterTemplate.Ship), refused.ParamName);
    }

    [Fact]
    public void ATemplateWithNoStartLocation_IsRefusedWhereItIsAuthored()
    {
        ArgumentNullException refused = Assert.Throws<ArgumentNullException>(
            () => new CharacterTemplate("id", "A name", Ship, null!, []));

        Assert.Equal(nameof(CharacterTemplate.StartLocation), refused.ParamName);
    }

    [Fact]
    public void ATemplateWithNoStartingInventory_IsRefusedWhereItIsAuthored()
    {
        ArgumentNullException refused = Assert.Throws<ArgumentNullException>(
            () => new CharacterTemplate("id", "A name", Ship, "start", null!));

        Assert.Equal(nameof(CharacterTemplate.StartingInventory), refused.ParamName);
    }

    [Fact]
    public void EveryShippedTemplate_IsAuthoredWellEnoughToBuildACharacterFrom()
    {
        // the guards above are only worth having if the content actually passes them, so the
        // roster is checked rather than assumed
        foreach (CharacterTemplate template in new BattleForceRoster().Templates)
        {
            Assert.NotNull(template.Id);
            Assert.NotNull(template.Name);
            Assert.NotNull(template.Ship);
            Assert.NotNull(template.StartLocation);
            Assert.NotNull(template.StartingInventory);
        }
    }
}
