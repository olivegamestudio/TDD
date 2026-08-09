namespace Pilgrimage.Tests;

/// <summary>
/// Covers the authoring end of a quest condition: what each kind carries, and which of them the
/// model refuses to be built at all.
/// </summary>
/// <remarks>
/// Nothing here evaluates a condition — Pilgrimage cannot, because a level and a faction are game
/// concepts it holds no knowledge of. <c>QuestConditionsTests</c> in the game's suite covers the
/// answering half.
/// </remarks>
public sealed class QuestConditionTests
{
    // ---- what each kind carries ----

    [Fact]
    public void PlayedAs_NamesTheCharacterAndMeasuresNothing()
    {
        QuestCondition condition = QuestCondition.PlayedAs("the-disgraced");

        Assert.Equal(QuestConditionKind.Character, condition.Kind);
        Assert.Equal("the-disgraced", condition.Subject);
        Assert.Equal(0, condition.Threshold);
    }

    [Fact]
    public void StandingWith_NamesTheGroupAndTheStandingItWants()
    {
        QuestCondition condition = QuestCondition.StandingWith("miners-guild", 500);

        Assert.Equal(QuestConditionKind.Faction, condition.Kind);
        Assert.Equal("miners-guild", condition.Subject);
        Assert.Equal(500, condition.Threshold);
    }

    [Fact]
    public void StandingWith_AcceptsAHostileStanding()
    {
        // a group that only deals with people they distrust is a group, and the sign is the
        // relationship rather than a number being wrong
        QuestCondition condition = QuestCondition.StandingWith("raiders", -250);

        Assert.Equal(-250, condition.Threshold);
    }

    [Fact]
    public void AtLeastLevel_CarriesTheLevelAndNamesNothing()
    {
        QuestCondition condition = QuestCondition.AtLeastLevel(5);

        Assert.Equal(QuestConditionKind.Level, condition.Kind);
        Assert.Equal(string.Empty, condition.Subject);
        Assert.Equal(5, condition.Threshold);
    }

    [Fact]
    public void AtLeastLevel_AcceptsTheLevelACharacterBeginsAt()
    {
        // level one is a real level, just an easy one to have reached
        Assert.Equal(1, QuestCondition.AtLeastLevel(1).Threshold);
    }

    [Fact]
    public void AfterCompleting_NamesTheQuestThatHasToComeFirst()
    {
        QuestCondition condition = QuestCondition.AfterCompleting("quest-1");

        Assert.Equal(QuestConditionKind.QuestPrerequisite, condition.Kind);
        Assert.Equal("quest-1", condition.Subject);
    }

    // ---- what the model refuses ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PlayedAs_RefusesAnIdentifierThatNamesNobody(string? characterId)
    {
        // it could never hold for anybody, so it is a quest silently unreachable by everyone
        Assert.ThrowsAny<ArgumentException>(() => QuestCondition.PlayedAs(characterId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StandingWith_RefusesAnIdentifierThatNamesNoGroup(string? groupId)
    {
        Assert.ThrowsAny<ArgumentException>(() => QuestCondition.StandingWith(groupId!, 100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AfterCompleting_RefusesAnIdentifierThatNamesNoQuest(string? questId)
    {
        // QuestLog.Register refuses exactly these, so nothing could ever be registered under one
        Assert.ThrowsAny<ArgumentException>(() => QuestCondition.AfterCompleting(questId!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AtLeastLevel_RefusesALevelBelowTheOneACharacterBeginsAt(int level)
    {
        // a condition nothing can fail is one the author meant something else by
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestCondition.AtLeastLevel(level));
    }

    // ---- conditions are values ----

    [Fact]
    public void TwoConditionsOfTheSameKindAndValueAreEqual()
    {
        Assert.Equal(QuestCondition.AtLeastLevel(5), QuestCondition.AtLeastLevel(5));
        Assert.Equal(
            QuestCondition.StandingWith("miners-guild", 500),
            QuestCondition.StandingWith("miners-guild", 500));
    }

    [Fact]
    public void ConditionsDifferingInAnythingAreNotEqual()
    {
        Assert.NotEqual(QuestCondition.AtLeastLevel(5), QuestCondition.AtLeastLevel(6));
        Assert.NotEqual(
            QuestCondition.StandingWith("miners-guild", 500),
            QuestCondition.StandingWith("raiders", 500));

        // the same subject read two different ways is two different conditions
        Assert.NotEqual(
            QuestCondition.PlayedAs("quest-1"),
            QuestCondition.AfterCompleting("quest-1"));
    }
}
