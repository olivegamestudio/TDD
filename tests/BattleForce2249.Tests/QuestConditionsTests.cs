using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the answering half of a gated quest: whether a <see cref="QuestCondition"/> holds for the
/// character being played. The model states the condition and knows what none of it means;
/// everything read here hangs off <see cref="Character"/>.
/// </summary>
public sealed class QuestConditionsTests
{
    readonly QuestLog _quests = new();

    Character CreateCharacter() => new(new TestRoster().Starting, _quests);

    static QuestDefinition Definition(string id) =>
        new(id, $"Quest {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));

    // ---- character ----

    [Fact]
    public void ACharacterConditionHolds_ForTheCharacterItNames()
    {
        Character character = CreateCharacter();

        Assert.True(QuestConditions.Holds(QuestCondition.PlayedAs(TestRoster.TemplateId), character));
    }

    [Fact]
    public void ACharacterConditionDoesNotHold_ForAnybodyElse()
    {
        Character character = CreateCharacter();

        Assert.False(QuestConditions.Holds(QuestCondition.PlayedAs("somebody-else"), character));
    }

    [Fact]
    public void ACharacterConditionIsMatchedExactly()
    {
        // an identifier is not text; it is matched the way it was written rather than the way the
        // player's culture would compare two words
        Character character = CreateCharacter();

        Assert.False(QuestConditions.Holds(
            QuestCondition.PlayedAs(TestRoster.TemplateId.ToUpperInvariant()), character));
    }

    // ---- level ----

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void ALevelConditionHolds_OnlyOnceTheCharacterHasReachedIt(int required, bool expected)
    {
        // a character begins at level one
        Character character = CreateCharacter();

        Assert.Equal(expected, QuestConditions.Holds(QuestCondition.AtLeastLevel(required), character));
    }

    [Fact]
    public void ALevelConditionOpens_WhenTheCharacterAdvancesIntoIt()
    {
        Character character = CreateCharacter();
        QuestCondition condition = QuestCondition.AtLeastLevel(3);

        Assert.False(QuestConditions.Holds(condition, character));

        character.Progression.Advance(0);
        character.Progression.Advance(0);

        Assert.Equal(3, character.Progression.Level);
        Assert.True(QuestConditions.Holds(condition, character));
    }

    // ---- faction ----

    [Fact]
    public void AFactionConditionDoesNotHold_ForAGroupTheCharacterHasNeverDealtWith()
    {
        // a stranger stands at zero, which is short of a quest wanting friendship
        Character character = CreateCharacter();

        Assert.False(QuestConditions.Holds(QuestCondition.StandingWith("miners-guild", 100), character));
    }

    [Fact]
    public void AFactionConditionAskingForNothingHolds_EvenForAStranger()
    {
        Character character = CreateCharacter();

        Assert.True(QuestConditions.Holds(QuestCondition.StandingWith("miners-guild", 0), character));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(101, true)]
    public void AFactionConditionHolds_AtTheStandingItAsksForAndAbove(int standing, bool expected)
    {
        Character character = CreateCharacter();
        character.Reputation.Adjust("miners-guild", standing);

        Assert.Equal(
            expected,
            QuestConditions.Holds(QuestCondition.StandingWith("miners-guild", 100), character));
    }

    [Fact]
    public void AFactionConditionReadsTheGroupItNames_AndNoOther()
    {
        Character character = CreateCharacter();
        character.Reputation.Adjust("raiders", 5_000);

        Assert.False(QuestConditions.Holds(QuestCondition.StandingWith("miners-guild", 100), character));
    }

    // ---- quest prerequisite ----

    [Fact]
    public void APrerequisiteDoesNotHold_WhileTheQuestItNamesIsUnfinished()
    {
        Quest first = _quests.Register(Definition("quest-1"));
        Character character = CreateCharacter();
        QuestCondition condition = QuestCondition.AfterCompleting("quest-1");

        Assert.False(QuestConditions.Holds(condition, character));

        first.Start();

        Assert.False(QuestConditions.Holds(condition, character));
    }

    [Fact]
    public void APrerequisiteHolds_OnceTheQuestItNamesIsComplete()
    {
        Quest first = _quests.Register(Definition("quest-1"));
        Character character = CreateCharacter();

        first.Start();
        first.Complete();

        Assert.True(QuestConditions.Holds(QuestCondition.AfterCompleting("quest-1"), character));
    }

    [Fact]
    public void APrerequisiteDoesNotHold_WhenItNamesAQuestThisBuildNoLongerShips()
    {
        // the safe answer of the two: a chain whose first link has been dropped stays shut rather
        // than opening a quest the player was never meant to reach
        Character character = CreateCharacter();

        Assert.False(QuestConditions.Holds(QuestCondition.AfterCompleting("quest-gone"), character));
    }

    // ---- all of them, or none to ask ----

    [Fact]
    public void AQuestNobodyGatedIsOneAnybodyCanPlay()
    {
        Assert.True(QuestConditions.Hold([], CreateCharacter()));
    }

    [Fact]
    public void EveryConditionHasToHold()
    {
        Quest first = _quests.Register(Definition("quest-1"));
        Character character = CreateCharacter();
        QuestCondition[] gate =
        [
            QuestCondition.PlayedAs(TestRoster.TemplateId),
            QuestCondition.AfterCompleting("quest-1"),
        ];

        // the character is right; the quest before it is not finished
        Assert.False(QuestConditions.Hold(gate, character));

        first.Start();
        first.Complete();

        Assert.True(QuestConditions.Hold(gate, character));
    }

    [Fact]
    public void OneConditionFailingIsEnoughToShutTheGate()
    {
        Character character = CreateCharacter();

        Assert.False(QuestConditions.Hold(
            [QuestCondition.PlayedAs(TestRoster.TemplateId), QuestCondition.AtLeastLevel(9)],
            character));
    }

    // ---- what it refuses ----

    [Fact]
    public void RefusesAConditionThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(() => QuestConditions.Holds(null!, CreateCharacter()));
    }

    [Fact]
    public void RefusesACharacterThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(
            () => QuestConditions.Holds(QuestCondition.AtLeastLevel(1), null!));
        Assert.Throws<ArgumentNullException>(() => QuestConditions.Hold([], null!));
    }
}
