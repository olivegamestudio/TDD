using Pilgrimage;

namespace BattleForce2249.Tests;

public sealed class SaveGameTests
{
    [Fact]
    public void ANullQuestListIsNoQuests_SoTheDeclarationOnQuestsHolds()
    {
        SaveGame save = new() { Quests = null! };

        Assert.NotNull(save.Quests);
        Assert.Empty(save.Quests);
    }

    [Fact]
    public void WithAlsoRefusesANullQuestList()
    {
        SaveGame save = new()
        {
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        };

        SaveGame cleared = save with { Quests = null! };

        Assert.Empty(cleared.Quests);
    }

    [Fact]
    public void ASaveWithANullQuestListEqualsOneWithNoQuests()
    {
        SaveGame withNull = new()
        {
            PlayerX = 12.5,
            PlayerY = -940.25,
            Quests = null!,
        };

        SaveGame withEmpty = new()
        {
            PlayerX = 12.5,
            PlayerY = -940.25,
            Quests = [],
        };

        Assert.Equal(withEmpty, withNull);
        Assert.Equal(withEmpty.GetHashCode(), withNull.GetHashCode());
    }

    [Fact]
    public void ASaveWithANullQuestListStillDiffersFromOneHoldingAQuest()
    {
        SaveGame withNull = new() { Quests = null! };

        SaveGame withQuest = new()
        {
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        };

        Assert.NotEqual(withQuest, withNull);
    }

    [Fact]
    public void TwoSavesHoldingTheSameQuestsAreEqual_ThoughTheListsAreNotTheSameList()
    {
        SaveGame first = new()
        {
            PlayerX = 3,
            PlayerY = 4,
            Quests =
            [
                new QuestProgress("quest-1", QuestState.Completed),
                new QuestProgress("quest-2", QuestState.Active),
            ],
        };

        SaveGame second = new()
        {
            PlayerX = 3,
            PlayerY = 4,
            Quests =
            [
                new QuestProgress("quest-1", QuestState.Completed),
                new QuestProgress("quest-2", QuestState.Active),
            ],
        };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void SavesDifferingOnlyInAQuestStateAreNotEqual()
    {
        SaveGame first = new()
        {
            Quests = [new QuestProgress("quest-1", QuestState.Completed)],
        };

        SaveGame second = new()
        {
            Quests = [new QuestProgress("quest-1", QuestState.Active)],
        };

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SavesDifferingOnlyInPositionAreNotEqual()
    {
        SaveGame first = new() { PlayerX = 1, PlayerY = 2 };
        SaveGame second = new() { PlayerX = 1, PlayerY = 3 };

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ASaveWithANullQuestListIsWrittenAsNoQuests_NotAsNull()
    {
        SaveGame saved = new()
        {
            PlayerX = 1,
            PlayerY = 2,
            Quests = null!,
        };

        string json = SaveGameSerializer.Serialize(saved);

        Assert.DoesNotContain("null", json);
        Assert.Equal(saved, SaveGameSerializer.Deserialize(json));
    }
}
