namespace Pilgrimage.Tests;

/// <summary>
/// Covers what a quest definition carries beside its triggers: the conditions gating each end, and
/// the difficulty the player is shown.
/// </summary>
public sealed class QuestDefinitionTests
{
    static QuestDefinition Quest() =>
        new(
            "quest-1",
            "A title",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));

    // ---- a quest nobody gated ----

    [Fact]
    public void AQuestWithNothingSaidAboutItCarriesNoConditions()
    {
        // conditions are something content adds, not something every quest has to opt out of
        QuestDefinition definition = Quest();

        Assert.Empty(definition.StartConditions);
        Assert.Empty(definition.EndConditions);
    }

    [Fact]
    public void AQuestWithNothingSaidAboutItIsLevelOne()
    {
        Assert.Equal(1, Quest().Level);
    }

    // ---- what content can author ----

    [Fact]
    public void CarriesTheConditionsGatingEachEndSeparately()
    {
        QuestDefinition definition = Quest() with
        {
            StartConditions = [QuestCondition.AtLeastLevel(5), QuestCondition.AfterCompleting("quest-0")],
            EndConditions = [QuestCondition.StandingWith("miners-guild", 100)],
        };

        Assert.Equal(
            [QuestCondition.AtLeastLevel(5), QuestCondition.AfterCompleting("quest-0")],
            definition.StartConditions);
        Assert.Equal([QuestCondition.StandingWith("miners-guild", 100)], definition.EndConditions);
    }

    [Fact]
    public void CarriesTheDifficultyTheQuestIsMeantToBe()
    {
        Assert.Equal(12, (Quest() with { Level = 12 }).Level);
    }

    [Fact]
    public void KeepsTheConditionsItWasGiven_EvenIfTheListItWasGivenIsAddedToAfterwards()
    {
        // a definition is content that never changes at runtime, so the list is copied rather than
        // held — a list still in the caller's hands is one that could gain an entry the guards
        // below never saw
        List<QuestCondition> authored = [QuestCondition.AtLeastLevel(5)];
        QuestDefinition definition = Quest() with { StartConditions = authored };

        authored.Add(QuestCondition.AtLeastLevel(50));

        Assert.Equal([QuestCondition.AtLeastLevel(5)], definition.StartConditions);
    }

    // ---- what it refuses ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusesALevelBelowOne(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Quest() with { Level = level });
    }

    [Fact]
    public void RefusesAConditionListThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(() => Quest() with { StartConditions = null! });
        Assert.Throws<ArgumentNullException>(() => Quest() with { EndConditions = null! });
    }

    [Fact]
    public void RefusesAConditionThatIsNotThere()
    {
        // refused where the quest is authored rather than on the frame that happens to ask
        Assert.Throws<ArgumentException>(
            () => Quest() with { StartConditions = [QuestCondition.AtLeastLevel(5), null!] });
        Assert.Throws<ArgumentException>(
            () => Quest() with { EndConditions = [null!] });
    }
}
