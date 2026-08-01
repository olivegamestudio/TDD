using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Playability cover for the quests the game ships: that each one can be reached, played through
/// and finished by flying, and that finishing it sticks.
/// </summary>
/// <remarks>
/// <para>
/// The other quest tests each hold one part of this still. <see cref="BattleForceCampaignTests"/>
/// covers what is authored, <see cref="BattleForceWorldTests"/> covers where the markers stand,
/// <see cref="QuestProximityWatcherTests"/> covers the rule being applied, and
/// <see cref="GameSessionTests"/> covers a quest's state being persisted — each against a part of
/// the game, and two of them against quests invented for the test. None of them would notice a
/// campaign whose pieces are all individually correct and cannot be played end to end.
/// </para>
/// <para>
/// So these run the shipping composition, from the menu to a completed quest, with a pilot at the
/// controls and nothing reaching in to move the player. Every assertion is on a quest id or a
/// quest state and never on a title, because a quest is the same quest in every language.
/// </para>
/// <para>
/// The campaign is walked rather than named, so a quest added later is covered by these the day it
/// is authored rather than the day someone remembers to write a test for it.
/// </para>
/// </remarks>
public sealed class QuestPlayabilityTests : GameplayTestBase
{
    /// <summary>
    /// How long a leg of a quest — reaching its start marker, then its end marker — is given
    /// before it is called unplayable. A minute of flying at 200 units a second is 12,000 units,
    /// which is a good deal further than anything the campaign has ever authored.
    /// </summary>
    const int FramesPerLeg = 60 * 60;

    static QuestMarkers MarkersFor(IWorld world, string questId) =>
        world.QuestMarkers.SingleOrDefault(markers => markers.QuestId == questId)
        ?? throw new InvalidOperationException(
            $"'{questId}' has no markers in the world, so nothing can ever fire its triggers.");

    /// <summary>
    /// Registers a pilot that flies at whatever it is pointed at, and hands it back so the test can
    /// point it.
    /// </summary>
    SeekingPilot StartTheGameWithASeekingPilot(out IHost host)
    {
        host = StartTheGame(services => services.AddSingleton<IShipInput>(provider =>
            new SeekingPilot(
                provider.GetRequiredService<IGameSession>().Player,
                provider.GetRequiredService<ShipMovement>())));

        return (SeekingPilot)Resolve<IShipInput>();
    }

    // ---- reachable from where the player actually is ----

    [Fact]
    public void EveryQuestCanBeBegun_ByTheOnlyThingThatBeginsQuests()
    {
        // proximity is the only way a quest starts, and the watcher only starts one that auto
        // starts. A quest authored with AutoStarts false would sit in the log forever, because
        // nothing in the game accepts a quest — there is no accept path and no screen to take it
        // on. When one arrives, this is the assertion to revisit rather than delete.
        Assert.All(
            new BattleForceCampaign().Quests,
            quest => Assert.True(
                quest.AutoStarts,
                $"'{quest.Id}' does not auto start, and nothing in the game can accept a quest"));
    }

    [Fact]
    public void TheGameOpensWithSomethingToDo()
    {
        // the campaign has to have a way in. The opening quest's only prerequisite is launching
        // the game, so a new game must leave the player inside its start trigger — if the spawn
        // point drifted out of range, the game would open on nothing to do and nothing that ever
        // happens afterwards would put that right.
        IHost host = StartTheGame();

        Play(host, frames: 1);

        Assert.NotEmpty(Session.Quests.Active);
    }

    // ---- played end to end, by flying ----

    [Fact]
    public void EveryQuestInTheCampaign_CanBeFlownFromItsStartMarkerToCompletion()
    {
        SeekingPilot pilot = StartTheGameWithASeekingPilot(out IHost host);
        Assert.True(Session.IsReady, "the game never became ready to play");

        IWorld world = Resolve<IWorld>();

        foreach (QuestDefinition definition in Resolve<ICampaign>().Quests)
        {
            QuestMarkers markers = MarkersFor(world, definition.Id);
            Quest quest = Session.Quests.Find(definition.Id)
                ?? throw new InvalidOperationException(
                    $"'{definition.Id}' is in the campaign but was not registered on a new game.");

            pilot.Target = markers.Start;
            Assert.True(
                PlayUntil(host, () => quest.State is not QuestState.NotStarted, FramesPerLeg),
                $"'{definition.Id}' never started: the player flew to {Session.Player.Position} "
                + $"and its start marker is at {markers.Start}.");

            pilot.Target = markers.End;
            Assert.True(
                PlayUntil(host, () => quest.IsCompleted, FramesPerLeg),
                $"'{definition.Id}' started but never completed: the player flew to "
                + $"{Session.Player.Position} and its end marker is at {markers.End}.");
        }

        // and the whole campaign is behind the player, with nothing left half done
        IEnumerable<string> everyQuest = Resolve<ICampaign>().Quests.Select(quest => quest.Id).Order();
        IEnumerable<string> completed = Session.Quests.Completed.Select(quest => quest.Id).Order();

        Assert.Equal(everyQuest, completed);
        Assert.Empty(Session.Quests.Active);
    }

    [Fact]
    public void NoQuestInTheCampaign_FinishesItself()
    {
        // the other half of playable: a quest that completes without being played is not a quest.
        // A minute at a dead stick, which is long enough that an end trigger authored on top of a
        // start marker — or a marker left at the origin by mistake — would have fired by now.
        IHost host = StartTheGame();

        Play(host, frames: FramesPerLeg);

        Assert.Empty(Session.Quests.Completed);
    }

    // ---- and it sticks ----

    [Fact]
    public async Task CompletingAQuestByFlying_IsWrittenToTheSave()
    {
        // a quest the player finishes and the game forgets is not a quest they finished. The
        // session's saving is covered against quests invented for the test; this is the shipping
        // campaign, completed by playing.
        InMemorySaveProgressService saves = new();
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services =>
        {
            services.AddSingleton<ISaveProgressService>(saves);
            services.AddSingleton<IShipInput>(pilot);
        });

        Assert.True(
            PlayUntil(host, () => Session.Quests.Completed.Any()),
            "flying straight forward from a new game never completed quest 1");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    [Fact]
    public async Task AQuestCompletedByFlying_IsNotHandedBackWhenTheGameIsResumed()
    {
        InMemorySaveProgressService saves = new();
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services =>
        {
            services.AddSingleton<ISaveProgressService>(saves);
            services.AddSingleton<IShipInput>(pilot);
        });
        Assert.True(
            PlayUntil(host, () => Session.Quests.Completed.Any()),
            "flying straight forward from a new game never completed quest 1");
        await Session.PendingSave;

        // the player comes back tomorrow: a second session over the same save and the same content
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
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services =>
        {
            services.AddSingleton<ISaveProgressService>(saves);
            services.AddSingleton<IShipInput>(pilot);
        });

        // resumed where the save left off, and still mid quest rather than starting it again
        Assert.Equal(new Position(0, 700), Session.Player.Position);
        Assert.Single(Session.Quests.Active);

        Assert.True(
            PlayUntil(host, () => Session.Quests.Completed.Any()),
            "a game resumed 300 units short of the exit marker could not be flown to the end");
        await Session.PendingSave;

        Assert.Contains(
            new QuestProgress(BattleForceCampaign.Quest1Id, QuestState.Completed),
            saves.Saved!.Quests);
    }

    // ---- in any language ----

    [Fact]
    public void AQuestIsPlayedThroughInWhateverLanguageTheGameIsIn()
    {
        // a quest is identified by an id that is never translated, so a playthrough must not depend
        // on the language the game is running in. Any culture but the source one would do.
        using CultureScope _ = new("fr");

        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Assert.True(
            PlayUntil(host, () => Session.Quests.Completed.Any()),
            "quest 1 could not be completed with the game running in French");

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);

        // and the language really was in force, so this is not an English run wearing a French
        // label. What the translation actually says is GameTextTests' business, not this test's.
        Assert.NotEqual("Get out. The debris field is collapsing around you.", quest.Title);
    }
}
