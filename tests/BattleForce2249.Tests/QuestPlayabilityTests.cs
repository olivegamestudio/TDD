using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Playability cover for the quests the game actually ships: that each one can be begun, flown
/// through to the end, and that finishing it sticks.
/// </summary>
/// <remarks>
/// <para>
/// The existing quest tests each hold one part of this still. <see cref="BattleForceCampaignTests"/>
/// covers what is authored, <see cref="BattleForceWorldTests"/> covers where the markers stand,
/// <see cref="QuestProximityWatcherTests"/> covers the rule being applied, and
/// <see cref="GameSessionTests"/> and <see cref="SaveRecoveryAdversarialTests"/> cover a quest's
/// state surviving a save — the last two against quests invented for the test. None of them would
/// notice a campaign whose pieces are each correct and which cannot be played from one end to the
/// other.
/// </para>
/// <para>
/// So these run the shipping composition, from the menu to a completed quest, against the real
/// campaign and the real world. The campaign is walked rather than named, so a quest added later
/// is covered by these the day it is authored rather than the day someone remembers to write a
/// test for it.
/// </para>
/// <para>
/// Every assertion is on a quest id or a quest state and never on a title, because a quest is the
/// same quest in every language. What the titles say is <see cref="GameTextTests"/>' business.
/// </para>
/// </remarks>
public sealed class QuestPlayabilityTests : GameplayTestBase
{
    static QuestMarkers MarkersFor(IWorld world, string questId) =>
        world.QuestMarkers.SingleOrDefault(markers => markers.QuestId == questId)
        ?? throw new InvalidOperationException(
            $"'{questId}' has no markers in the world, so nothing can ever fire its triggers.");

    // ---- reachable from what comes before it ----

    [Fact]
    public void EveryQuestInTheCampaign_CanBeBegunByTheOnlyThingThatBeginsQuests()
    {
        // proximity is the only way a quest starts, and the watcher only starts one that auto
        // starts. A quest authored with AutoStarts false would sit in the log forever, because
        // nothing in the game accepts a quest — there is no accept path and no screen to take one
        // on. When that arrives, this is the assertion to revisit rather than to delete.
        Assert.All(
            new BattleForceCampaign().Quests,
            quest => Assert.True(
                quest.AutoStarts,
                $"'{quest.Id}' does not auto start, and nothing in the game can accept a quest."));
    }

    [Fact]
    public void TheGameOpensWithSomethingToDo()
    {
        // the campaign has to have a way in. The opening quest's only prerequisite is launching the
        // game, so a new game must leave the player already inside its start trigger — if the spawn
        // point drifted out of range the game would open on nothing to do, and nothing that happens
        // afterwards would put that right.
        IHost host = StartTheGame();

        Play(host, frames: 1);

        Assert.NotEmpty(Session.Quests.Active);
    }

    // ---- played end to end, by flying ----

    [Fact]
    public void EveryQuestInTheCampaign_CanBeFlownFromItsStartMarkerToCompletion()
    {
        IHost host = StartTheGame();
        Assert.True(Session.IsReady, "the game never became ready to play.");

        IWorld world = Resolve<IWorld>();
        IReadOnlyList<QuestDefinition> campaign = Resolve<ICampaign>().Quests;

        foreach (QuestDefinition definition in campaign)
        {
            QuestMarkers markers = MarkersFor(world, definition.Id);
            Quest quest = Session.Quests.Find(definition.Id)
                ?? throw new InvalidOperationException(
                    $"'{definition.Id}' is in the campaign but was not registered on a new game.");

            Assert.True(
                FlyTowards(host, markers.Start, () => quest.State is not QuestState.NotStarted),
                $"'{definition.Id}' never started: the player flew to {Session.Player.Position} "
                + $"and its start marker is at {markers.Start}.");

            Assert.True(
                FlyTowards(host, markers.End, () => quest.IsCompleted),
                $"'{definition.Id}' started but never completed: the player flew to "
                + $"{Session.Player.Position} and its end marker is at {markers.End}.");
        }

        // and the whole campaign is behind the player, with nothing left half done
        Assert.Equal(
            campaign.Select(quest => quest.Id).Order(),
            Session.Quests.Completed.Select(quest => quest.Id).Order());
        Assert.Empty(Session.Quests.Active);
    }

    [Fact]
    public void NoQuestInTheCampaign_FinishesItself()
    {
        // the other half of playable: a quest that completes without being played is not a quest.
        // A minute at a dead stick is long enough that an end trigger authored on top of a start
        // marker — or a marker left at the origin by mistake — would have fired by now.
        IHost host = StartTheGame();

        Play(host, FramesPerLeg);

        Assert.Empty(Session.Quests.Completed);
    }

    // ---- and finishing it sticks ----

    [Fact]
    public async Task CompletingAQuestByFlying_IsWrittenToTheSave()
    {
        // a quest the player finishes and the game forgets is not a quest they finished
        InMemorySaveProgressService saves = new();
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        Assert.True(
            FlyTowards(host, markers.End, () => Session.Quests.Completed.Any()),
            "flying to the exit marker from a new game never completed quest 1.");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    [Fact]
    public async Task AQuestCompletedByFlying_IsNotHandedBackWhenTheGameIsResumed()
    {
        InMemorySaveProgressService saves = new();
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        Assert.True(
            FlyTowards(host, markers.End, () => Session.Quests.Completed.Any()),
            "flying to the exit marker from a new game never completed quest 1.");
        await Session.PendingSave;

        // the player comes back tomorrow: a second session over the same save
        GameSession resumed = new(saves, new BattleForceCampaign(), new BattleForceWorld());
        int started = 0;
        resumed.Quests.QuestStarted += (_, _) => started++;
        await resumed.Continue();

        Assert.Equal(QuestState.Completed, resumed.Quests.Find(BattleForceCampaign.Quest1Id)!.State);
        Assert.Equal(0, started);
    }

    [Fact]
    public async Task AGameResumedPartWayThroughAQuest_CanStillBeFlownToTheEnd()
    {
        // the save carries where the player is, never how fast they were going, so a resumed player
        // is placed rather than flown. They still have to be able to finish what they were doing.
        InMemorySaveProgressService saves = new()
        {
            Content = SaveGameSerializer.Serialize(new SaveGame
            {
                PlayerY = 700,
                Quests = [new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Active)],
            }),
        };
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        // resumed where the save left off, and still mid quest rather than starting it again
        Assert.Equal(new Position(0, 700), Session.Player.Position);
        Assert.Single(Session.Quests.Active);

        Assert.True(
            FlyTowards(host, markers.End, () => Session.Quests.Completed.Any()),
            "a game resumed 300 units short of the exit marker could not be flown to the end.");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    // ---- from a save with drift in it ----

    /// <summary>
    /// The shape of the save from the report on #44: real progress standing beside a quest entry
    /// whose identifier is blank. Both save edges call such an entry drift and skip it, so what the
    /// player gets back is the progress next to it — and that has to be a game, not just a state.
    /// </summary>
    const string SaveHoldingAnEntryThatNamesNoQuest = """
    { "PlayerX": 0, "PlayerY": 700,
      "Quests": [ { "QuestId": "quest-1", "State": "Active" },
                  { "QuestId": "  ",      "State": "Active" } ] }
    """;

    [Fact]
    public async Task AGameResumedFromASaveHoldingAnEntryThatNamesNoQuest_CanStillBeFlownToTheEnd()
    {
        // Skipping the entry is only worth anything if what survives is a game. The unit cover says
        // the session comes back ready with the quest in the state the file gave it; that is not the
        // same claim as the player being able to finish what they were part way through, which is
        // what the file was keeping for them.
        InMemorySaveProgressService saves = new() { Content = SaveHoldingAnEntryThatNamesNoQuest };
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        // resumed where the save left off, mid quest, with the entry that names nothing dropped
        Assert.Equal(new Position(0, 700), Session.Player.Position);
        Quest quest = Assert.Single(Session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);

        Assert.True(
            FlyTowards(host, markers.End, () => quest.IsCompleted),
            "a quest resumed beside an entry naming no quest could not be flown to its end: the "
            + $"player flew to {Session.Player.Position} and the exit marker is at {markers.End}.");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    [Fact]
    public async Task AQuestCompletedInASaveHoldingAnEntryThatNamesNoQuest_IsNotHandedBack()
    {
        // The half the report was actually about: a finished quest surviving the junk beside it. It
        // has to survive being played on from, not just being loaded — the completed quest's start
        // marker is the position a new game spawns on, so a quest restored as anything less than
        // completed would be handed back the moment the player flew home, and saved that way.
        InMemorySaveProgressService saves = new()
        {
            Content = """
            { "PlayerX": 0, "PlayerY": 700,
              "Quests": [ { "QuestId": "quest-1", "State": "Completed" },
                          { "QuestId": "  ",      "State": "Active"    } ] }
            """,
        };
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        int handedBack = 0;
        Session.Quests.QuestStarted += (_, _) => handedBack++;

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);

        // fly all the way back over the marker that starts it, which is where a new game begins
        Assert.True(
            FlyTowards(host, markers.Start, () => Session.Player.Position.DistanceTo(markers.Start) < 1),
            "the player could not be flown back to the start marker.");
        await Session.PendingSave;

        Assert.Equal(0, handedBack);
        Assert.True(quest.IsCompleted);
        Assert.Empty(Session.Quests.Active);
        // and nothing was written, so a resumed campaign is not re-saved by being played through
        Assert.Equal(0, saves.SaveCount);
    }

    [Fact]
    public async Task ASaveRefusedForAnEntryThatIsNotThere_LeavesACampaignThatCanStillBeFlown()
    {
        // The other side of the same line, and the one that decides whether refusing is survivable.
        // An entry that is not an entry still costs the whole file, so the player loses the campaign
        // in it — what they must not also lose is the game. A new game is only a recovery if it can
        // be played, so this flies the replacement rather than asserting the session came back ready.
        InMemorySaveProgressService saves = new()
        {
            Content = """
            { "PlayerX": 0, "PlayerY": 700,
              "Quests": [ { "QuestId": "quest-1", "State": "Completed" }, null ] }
            """,
        };
        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        // the refused file is behind them: back at the world's start, with the campaign to play
        Assert.Equal(Resolve<IWorld>().PlayerStart, Session.Player.Position);
        Assert.Empty(Session.Quests.Completed);

        Assert.True(
            FlyTowards(host, markers.End, () => Session.Quests.Completed.Any()),
            "the new game that replaced a refused save could not be flown to the end of quest 1.");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    // ---- in whatever language the game is in ----

    [Fact]
    public void AQuestIsPlayedThroughInWhateverLanguageTheGameIsIn()
    {
        // A quest is identified by an id that is never translated, so a playthrough must not depend
        // on the language the game runs in. It could: the campaign reads its titles through
        // GameText every time it is asked for its quests, so a language the text cannot be read in
        // would throw where the quests are registered and leave the player with no game at all.
        using CultureScope _ = new("fr");

        IHost host = StartTheGame();
        QuestMarkers markers = MarkersFor(Resolve<IWorld>(), BattleForceCampaign.Quest1Id);

        Assert.True(
            FlyTowards(host, markers.End, () => Session.Quests.Completed.Any()),
            "quest 1 could not be completed with the game running in French.");

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);

        // and the language really was in force, so this is not an English run wearing a French
        // label. What the translation says is GameTextTests' business, not this test's.
        Assert.NotEqual("Get out. The debris field is collapsing around you.", quest.Title);
    }
}
