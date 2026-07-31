using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the quests Battle Force 2249 ships with. Where their markers stand is a world fact,
/// covered by <see cref="BattleForceWorldTests"/>.
/// </summary>
public sealed class BattleForceCampaignTests
{
    readonly BattleForceCampaign _campaign = new();

    static QuestDefinition Quest1(BattleForceCampaign campaign) =>
        campaign.Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    [Fact]
    public void ContainsQuest1()
    {
        Assert.Contains(_campaign.Quests, quest => quest.Id == BattleForceCampaign.Quest1Id);
    }

    [Fact]
    public void Quest1_HasTheTitleFromTheDesign()
    {
        // the title is translated, so pin the language the design line is written in; the
        // translations themselves are covered by GameTextTests
        using CultureScope _ = new("en");

        Assert.Equal("Get out. The debris field is collapsing around you.", Quest1(new BattleForceCampaign()).Title);
    }

    [Fact]
    public void Quest1_AutoStarts()
    {
        Assert.True(Quest1(_campaign).AutoStarts);
    }

    [Fact]
    public void Quest1_BeginsAndEndsOnProximity()
    {
        QuestDefinition quest = Quest1(_campaign);

        Assert.Equal(QuestTriggerKind.Proximity, quest.Start.Kind);
        Assert.Equal(QuestTriggerKind.Proximity, quest.End.Kind);
    }

    [Fact]
    public void Quest1_TriggersAtAUsableDistance()
    {
        // a zero distance would demand an exact position, which a moving ship would fly straight past
        QuestDefinition quest = Quest1(_campaign);

        Assert.True(quest.Start.Distance > 0);
        Assert.True(quest.End.Distance > 0);
    }

    [Fact]
    public void EveryQuestHasAUniqueId()
    {
        Assert.Equal(_campaign.Quests.Count, _campaign.Quests.Select(quest => quest.Id).Distinct().Count());
    }

    [Fact]
    public void EveryQuestHasATitle()
    {
        Assert.All(_campaign.Quests, quest => Assert.False(string.IsNullOrWhiteSpace(quest.Title)));
    }
}
