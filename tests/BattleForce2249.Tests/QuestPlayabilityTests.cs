using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Flies every quest the game ships from beginning to end, headlessly, and asserts it can still be
/// finished. Where the other suites take a quest apart — <see cref="QuestProximityWatcherTests"/>
/// for the measuring, <see cref="GameSessionTests"/> for the saving, <see cref="QuestLogTests"/>
/// for the model — this one only asks the question a player would: can it be played to the end?
/// </summary>
/// <remarks>
/// <para>
/// The campaign and the world here are the shipped <see cref="BattleForceCampaign"/> and
/// <see cref="BattleForceWorld"/>, not stand-ins. That is the point of the suite: every other test
/// builds its own quest at its own distances from its own markers, so a quest whose authored
/// trigger distance no longer reaches the marker the world actually places would pass all of them
/// and be unfinishable in the game. Nothing renders — the quest is driven through
/// <see cref="QuestProximityWatcher"/>, which is the whole of what a frame does to a quest — so a
/// container with no display can still answer the question.
/// </para>
/// <para>
/// The playthroughs are <see cref="TheoryAttribute"/>s over the campaign's quest ids rather than
/// tests written per quest, so a quest added to <see cref="BattleForceCampaign"/> is flown from the
/// moment it ships instead of when someone remembers to write the test.
/// </para>
/// <para>
/// Assertions are on identifiers and <see cref="QuestState"/>, never on titles: a title is
/// translated, and a quest that is playable in English has to be playable in Japanese.
/// <see cref="EveryQuest_PlaysTheSame_WhateverLanguageItIsPlayedIn"/> is the one that pins that.
/// </para>
/// <para>
/// Quests currently carry no rewards and no follow-on unlock — completing one grants nothing and
/// opens nothing, because the campaign is one quest long. That is asserted rather than assumed in
/// <see cref="CompletingTheCampaign_LeavesNoQuestStranded"/>, so the day a quest does grant
/// something, this suite fails and gets extended instead of quietly not covering it.
/// </para>
/// </remarks>
public sealed class QuestPlayabilityTests
{
    /// <summary>How far the player travels in one frame. Any speed the movement system settles on
    /// has to be small enough that a trigger is not flown straight past between two frames.</summary>
    const double UnitsPerFrame = 10;

    /// <summary>Frames a single leg may take before the quest is called unfinishable rather than
    /// slow. At ten units a frame this is a hundred thousand units of travel.</summary>
    const int FrameLimit = 10_000;

    readonly ICampaign _campaign = new BattleForceCampaign();
    readonly ICharacterRoster _roster = new BattleForceRoster();
    readonly IWorld _world = new BattleForceWorld();
    readonly InMemorySaveProgressService _saves = new();

    /// <summary>
    /// Every quest id in the shipped campaign. Ids, not titles — the data a theory is named after
    /// ends up in test output, and an id reads the same in every language.
    /// </summary>
    public static TheoryData<string> QuestIds =>
        [.. new BattleForceCampaign().Quests.Select(quest => quest.Id)];

    GameSession CreateSession() => new(_saves, _campaign, _roster, _world);

    GameSession CreateSession(ISaveProgressService saves) => new(saves, _campaign, _roster, _world);

    QuestProximityWatcher CreateWatcher() => new(_world);

    QuestMarkers MarkersFor(string questId) =>
        _world.QuestMarkers.Single(markers => markers.QuestId == questId);

    /// <summary>
    /// Flies the player to <paramref name="target"/> at <see cref="UnitsPerFrame"/> a frame, driving
    /// the watcher on every frame — the same thing <see cref="GameScreen.Update"/> does, minus the
    /// screen. The watcher runs once before the first step as well, because a player who is already
    /// standing on the marker has still arrived at it.
    /// </summary>
    static void FlyTo(GameSession session, QuestProximityWatcher watcher, Position target)
    {
        watcher.Update(session.Quests, session.Player.Position);

        for (int frame = 0; session.Player.Position.DistanceTo(target) > UnitsPerFrame; frame++)
        {
            Assert.True(frame < FrameLimit, $"the player never reached {target}");

            Position at = session.Player.Position;
            double distance = at.DistanceTo(target);
            session.Player.MoveBy(
                (target.X - at.X) / distance * UnitsPerFrame,
                (target.Y - at.Y) / distance * UnitsPerFrame);

            watcher.Update(session.Quests, session.Player.Position);
        }
    }

    /// <summary>
    /// Takes a quest from wherever the player is to completion: to its start marker, accepting it
    /// if it does not begin on its own, and then on to its end marker.
    /// </summary>
    void Play(GameSession session, QuestProximityWatcher watcher, string questId)
    {
        Quest quest = session.Quests.Find(questId)!;
        QuestMarkers markers = MarkersFor(questId);

        FlyTo(session, watcher, markers.Start);

        if (!quest.Definition.AutoStarts)
        {
            // the half of starting a quest that is the player's, not the world's
            quest.Start();
        }

        Assert.Equal(QuestState.Active, quest.State);

        FlyTo(session, watcher, markers.End);
    }

    // ---- playable from a new game ----

    [Theory]
    [MemberData(nameof(QuestIds))]
    public async Task EveryQuest_CanBeFlownFromANewGameToCompletion(string questId)
    {
        GameSession session = CreateSession();
        QuestProximityWatcher watcher = CreateWatcher();

        await session.StartNewGame();

        Quest quest = session.Quests.Find(questId)!;
        Assert.Equal(QuestState.NotStarted, quest.State);

        Play(session, watcher, questId);

        Assert.Equal(QuestState.Completed, quest.State);
    }

    [Theory]
    [MemberData(nameof(QuestIds))]
    public async Task EveryQuestsStartMarker_IsReachableFromWhereANewGameBegins(string questId)
    {
        // A quest whose start marker cannot be flown to from the spawn point is content the player
        // can never see, however correct the quest itself is. This is the prerequisite check: for a
        // campaign of one quest the prerequisite is simply that the game has begun.
        GameSession session = CreateSession();
        QuestProximityWatcher watcher = CreateWatcher();

        await session.StartNewGame();
        FlyTo(session, watcher, MarkersFor(questId).Start);

        Quest quest = session.Quests.Find(questId)!;
        Assert.True(
            quest.IsActive || !quest.Definition.AutoStarts,
            $"'{questId}' auto-starts but standing on its start marker did not begin it");
    }

    [Fact]
    public async Task CompletingTheCampaign_LeavesNoQuestStranded()
    {
        // Every quest finished, nothing left running, and nothing left that the player was never
        // able to reach. Also the follow-on check: no quest is waiting on another to unlock it,
        // because completing them all in campaign order leaves none behind.
        GameSession session = CreateSession();
        QuestProximityWatcher watcher = CreateWatcher();

        await session.StartNewGame();

        foreach (QuestDefinition definition in _campaign.Quests)
        {
            Play(session, watcher, definition.Id);
        }

        Assert.Empty(session.Quests.Active);
        Assert.Equal(
            _campaign.Quests.Select(quest => quest.Id).Order(),
            session.Quests.Completed.Select(quest => quest.Id).Order());
    }

    // ---- playable from a resumed save ----

    [Theory]
    [MemberData(nameof(QuestIds))]
    public async Task EveryQuest_StaysCompleted_WhenTheGameIsResumed(string questId)
    {
        GameSession session = CreateSession();
        await session.StartNewGame();
        Play(session, CreateWatcher(), questId);
        await session.PendingSave;

        // a fresh session over the same save, which is what launching the game again does
        GameSession resumed = CreateSession();
        QuestProximityWatcher watcher = CreateWatcher();
        await resumed.Continue();

        Quest quest = resumed.Quests.Find(questId)!;
        Assert.Equal(QuestState.Completed, quest.State);

        // and the frames that follow do not hand it back to the player to play again
        for (int frame = 0; frame < 10; frame++)
        {
            watcher.Update(resumed.Quests, resumed.Player.Position);
        }

        Assert.Equal(QuestState.Completed, quest.State);
        Assert.Empty(resumed.Quests.Active);
    }

    [Fact]
    public async Task Quest1_IsStillCompletable_WhenTheGameIsResumedPartWayThrough()
    {
        // The shape a player actually saves in: quit half way down the debris field, come back, and
        // fly the rest of it. Nothing else flies a quest that a save put in the middle of.
        _saves.Content = SaveGameSerializer.Serialize(new SaveGame
        {
            PlayerY = 700,
            Quests = [new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Active)],
        });
        GameSession session = CreateSession();
        QuestProximityWatcher watcher = CreateWatcher();

        await session.Continue();
        Assert.Equal(new Position(0, 700), session.Player.Position);

        FlyTo(session, watcher, MarkersFor(BattleForceCampaign.Quest1Id).End);
        await session.PendingSave;

        Assert.True(session.Quests.Find(BattleForceCampaign.Quest1Id)!.IsCompleted);
        Assert.Equal(QuestState.Completed, _saves.Saved!.Quests.Single().State);
    }

    // ---- playable from a save that names the quest twice (#72) ----

    [Theory]
    [InlineData(QuestState.Completed, QuestState.NotStarted)]
    [InlineData(QuestState.NotStarted, QuestState.Completed)]
    public async Task ASaveNamingQuest1Twice_ResumesAFinishedCampaign_WhicheverOrderTheEntriesAreIn(
        QuestState first, QuestState second)
    {
        // QuestLogTests pins the rule and SaveRecoveryAdversarialTests pins the reported file; this
        // asks what the player gets. A campaign they finished stays finished across the frames that
        // follow the load, rather than the watcher finding a quest to start again.
        GameSession session = CreateSession(Duplicate(first, second));
        QuestProximityWatcher watcher = CreateWatcher();

        await session.Continue();

        Quest quest = session.Quests.Find(BattleForceCampaign.Quest1Id)!;
        Assert.Equal(QuestState.Completed, quest.State);

        for (int frame = 0; frame < 100; frame++)
        {
            watcher.Update(session.Quests, session.Player.Position);
            session.Player.MoveBy(0, UnitsPerFrame);
        }

        Assert.Equal(QuestState.Completed, quest.State);
        Assert.Empty(session.Quests.Active);
    }

    [Theory]
    [InlineData(QuestState.Active, QuestState.NotStarted)]
    [InlineData(QuestState.NotStarted, QuestState.Active)]
    public async Task ASaveNamingQuest1Twice_LeavesItFinishableByFlying_WhenTheFurthestIsUnfinished(
        QuestState first, QuestState second)
    {
        // The other half of the duplicate rule, and the half a state assertion cannot answer: a
        // quest resumed from a duplicate entry is not merely in the right state, it still responds
        // to the world. Whichever line the file leads with, the player flies out of the debris
        // field and the quest finishes.
        GameSession session = CreateSession(Duplicate(first, second));
        QuestProximityWatcher watcher = CreateWatcher();

        await session.Continue();
        Assert.Equal(QuestState.Active, session.Quests.Find(BattleForceCampaign.Quest1Id)!.State);

        FlyTo(session, watcher, MarkersFor(BattleForceCampaign.Quest1Id).End);

        Assert.True(session.Quests.Find(BattleForceCampaign.Quest1Id)!.IsCompleted);
    }

    /// <summary>
    /// A save naming quest 1 twice, in the order given. The player is left short of the exit so the
    /// quest is finishable by flying rather than finished by arriving.
    /// </summary>
    static ISaveProgressService Duplicate(QuestState first, QuestState second) =>
        new InMemorySaveProgressService
        {
            Content = SaveGameSerializer.Serialize(new SaveGame
            {
                PlayerY = 700,
                Quests =
                [
                    new QuestProgress(BattleForceCampaign.Quest1Id, first),
                    new QuestProgress(BattleForceCampaign.Quest1Id, second),
                ],
            }),
        };

    // ---- playable in every language ----

    [Theory]
    [MemberData(nameof(QuestIds))]
    public async Task EveryQuest_PlaysTheSame_WhateverLanguageItIsPlayedIn(string questId)
    {
        // A quest is saved under an id and driven by distances; neither is translated. If a title
        // ever leaked into either, a save written in one language would not load in another — so
        // the quest is finished in French and resumed in Japanese, and has to still be finished.
        GameSession session = CreateSession();
        using (new CultureScope("fr"))
        {
            await session.StartNewGame();
            Play(session, CreateWatcher(), questId);
            await session.PendingSave;
        }

        using (new CultureScope("ja"))
        {
            GameSession resumed = CreateSession();
            await resumed.Continue();

            Assert.Equal(QuestState.Completed, resumed.Quests.Find(questId)!.State);
        }
    }
}
