using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game content itself: the quests Battle Force 2249 ships with.
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
        // the title is translated now, so pin the language the design line is written in; the
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
    public void Quest1_StartsWhereANewGameStartsThePlayer()
    {
        // otherwise nothing would auto start it on a new game launch
        Assert.True(Quest1(_campaign).Start.IsReachedBy(_campaign.PlayerStart));
    }

    [Fact]
    public void Quest1_EndsForwardOfItsStart()
    {
        QuestDefinition quest = Quest1(_campaign);

        Assert.True(quest.End.Position.Y > quest.Start.Position.Y);
        Assert.False(quest.End.IsReachedBy(_campaign.PlayerStart));
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
