using Microsoft.Extensions.Logging.Abstractions;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Plays every quest the campaign ships, end to end, headlessly.
/// </summary>
/// <remarks>
/// <para>
/// The other quest tests each hold one part of a quest still — what is authored
/// (<see cref="BattleForceCampaignTests"/>), where the markers stand
/// (<see cref="BattleForceWorldTests"/>), the proximity rule being applied
/// (<see cref="QuestProximityWatcherTests"/>), the state being persisted
/// (<see cref="GameSessionTests"/>). None of them answers the question a player asks, which is
/// whether the quest can be reached, begun, flown and finished. That is what this covers, against
/// a real <see cref="GameSession"/> and a real save.
/// </para>
/// <para>
/// It walks <see cref="ICampaign.Quests"/> rather than naming quest 1, so a quest authored later
/// is covered the day it is written rather than the day someone remembers to add a test for it.
/// A quest that cannot be played fails here even if every part of it is individually correct.
/// </para>
/// <para>
/// Nothing is drawn and nothing is timed by a real clock: the game separates its logic from its
/// presentation, so the whole playthrough runs through the logic layer. Assertions are on quest
/// ids and <see cref="QuestState"/>, never on titles, because a quest has to be playable in every
/// language and a save written in one has to load in another.
/// </para>
/// </remarks>
public sealed class QuestPlayabilityTests
{
    /// <summary>
    /// A frame at 60Hz, the rate the game is played at.
    /// </summary>
    static readonly TimeSpan Frame = TimeSpan.FromSeconds(1 / 60.0);

    /// <summary>
    /// How fast the flights below travel, in world units a second. Ships and their physics are
    /// issue #3 and are not on this branch, so a playthrough flies at a plain constant speed —
    /// what is being asked here is whether the quest can be completed by covering the ground
    /// between its markers, which does not depend on how the ship accelerates.
    /// </summary>
    const double Speed = 200;

    readonly BattleForceCampaign _campaign = new();

    readonly BattleForceWorld _world = new();

    QuestMarkers MarkersFor(QuestDefinition quest) =>
        _world.QuestMarkers.Single(markers => markers.QuestId == quest.Id);

    /// <summary>
    /// Builds a session on a save that starts empty, so each test plays its own game.
    /// </summary>
    (GameSession Session, InMemorySaveProgressService Save) NewSession()
    {
        InMemorySaveProgressService save = new();
        return (new GameSession(save, _campaign, _world), save);
    }

    /// <summary>
    /// Flies a session's player, one frame at a time, driving its quests exactly as
    /// <see cref="GameScreen.Update"/> does: the position is read before the player is moved, and
    /// both ends of the frame's journey are handed to the watcher, so a marker passed within a
    /// frame counts as passed.
    /// </summary>
    /// <remarks>
    /// This stands in for the movement system, which is not built yet (#3). It is deliberately the
    /// caller that moves the player, because that is where movement will live — between the two
    /// lines of <see cref="GameScreen.Update"/> — and a playthrough that drove the watcher any
    /// other way would be testing a game nobody is going to ship.
    /// </remarks>
    sealed class Flight(GameSession session, QuestProximityWatcher watcher, TimeSpan frameTime)
    {
        /// <summary>
        /// A flight long enough to cross the world several times over. Reaching it means the
        /// player is not converging on the destination, which is a hang rather than a failure, so
        /// it is asserted on rather than looped forever.
        /// </summary>
        const int MaxFrames = 100_000;

        /// <summary>
        /// Gets how many frames have been flown, so a test can tell a playthrough from a quest
        /// that completed before anyone moved.
        /// </summary>
        public int Frames { get; private set; }

        /// <summary>
        /// Flies straight to <paramref name="destination"/>, arriving exactly on it.
        /// </summary>
        public Flight To(Position destination)
        {
            while (session.Player.Position != destination)
            {
                Assert.True(Frames < MaxFrames, "the player never reached the destination");

                Position from = session.Player.Position;
                double remaining = from.DistanceTo(destination);
                double fraction = Math.Min(Speed * frameTime.TotalSeconds / remaining, 1);

                session.Player.MoveTo(new Position(
                    from.X + ((destination.X - from.X) * fraction),
                    from.Y + ((destination.Y - from.Y) * fraction)));

                watcher.Update(session.Quests, from, session.Player.Position);
                Frames++;
            }

            return this;
        }

        /// <summary>
        /// Spends a frame going nowhere, which is how a quest whose marker the player is already
        /// standing on gets noticed.
        /// </summary>
        public Flight Hold()
        {
            watcher.Update(session.Quests, session.Player.Position, session.Player.Position);
            Frames++;

            return this;
        }
    }

    Flight FlightFor(GameSession session, TimeSpan? frameTime = null) =>
        new(session, new QuestProximityWatcher(_world), frameTime ?? Frame);

    /// <summary>
    /// Flies the whole campaign: every quest's start marker and then out through its end marker,
    /// in the order the campaign lists them.
    /// </summary>
    Flight FlyEveryQuest(GameSession session, TimeSpan? frameTime = null)
    {
        Flight flight = FlightFor(session, frameTime).Hold();

        foreach (QuestDefinition quest in _campaign.Quests)
        {
            QuestMarkers markers = MarkersFor(quest);

            // through the end marker rather than stopping dead on it: a player flies past their
            // objective and keeps going, and a quest that only finishes for someone who parks
            // exactly on the marker is not one that can be played
            flight.To(markers.Start).To(Beyond(markers.Start, markers.End, quest.End.Distance));
        }

        return flight;
    }

    // ---- can a quest be begun at all ----

    [Fact]
    public void EveryQuestInTheCampaign_CanBeBegunByPlaying()
    {
        // Nothing in the game can accept a quest: the proximity watcher is the only thing that
        // calls Quest.Start, and it will not start a quest that does not auto-start. So a quest
        // authored with AutoStarts: false today would be unplayable however well it was written,
        // and this is the assertion that says so before it ships rather than after.
        Assert.All(_campaign.Quests, quest => Assert.True(
            quest.AutoStarts,
            $"'{quest.Id}' does not auto-start, and there is no way for a player to accept a quest yet"));
    }

    [Fact]
    public async Task EveryQuestInTheCampaign_IsReachableFromWhereANewGameBegins()
    {
        // A quest whose start marker cannot be flown to from the spawn point is content nobody
        // will ever see. Flown, not placed — the player has to be able to get there.
        (GameSession session, _) = NewSession();
        await session.StartNewGame();
        Flight flight = FlightFor(session).Hold();

        foreach (QuestDefinition quest in _campaign.Quests)
        {
            flight.To(MarkersFor(quest).Start);

            Assert.Equal(QuestState.Active, session.Quests.Find(quest.Id)!.State);
        }
    }

    [Fact]
    public async Task Quest1_BeginsOnTheFirstFrameOfANewGame_WithoutThePlayerTouchingAnything()
    {
        // Quest 1's only prerequisite is launching the game, so this is its reachability check:
        // the screen the game is played on starts it, on its own, from a cold start.
        (GameSession session, _) = NewSession();
        GameScreen screen = new(session, new QuestProximityWatcher(_world), NullLogger<GameScreen>.Instance);

        screen.Enter();
        await screen.Started;
        screen.Update(Frame);

        Quest quest = Assert.Single(session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    // ---- can a quest be finished ----

    [Fact]
    public async Task EveryQuestInTheCampaign_CanBeFlownFromNotStartedToCompleted()
    {
        (GameSession session, _) = NewSession();
        await session.StartNewGame();
        Flight flight = FlightFor(session).Hold();

        foreach (QuestDefinition quest in _campaign.Quests)
        {
            QuestMarkers markers = MarkersFor(quest);
            Quest playing = session.Quests.Find(quest.Id)!;

            flight.To(markers.Start);
            Assert.Equal(QuestState.Active, playing.State);

            // flown through and past, not parked on
            flight.To(Beyond(markers.Start, markers.End, quest.End.Distance));
            Assert.Equal(QuestState.Completed, playing.State);
        }

        Assert.True(flight.Frames > 1, "no quest was actually flown");
    }

    [Fact]
    public async Task EveryQuestInTheCampaign_CanBeFlownByAPlayerWhoIsNotOnTheExactLine()
    {
        // Nobody flies through the exact point a marker stands on, so a playthrough down the line
        // through the markers proves less than it looks: every trigger wider than nothing fires
        // for a ship threading them perfectly. The slop here is the ground one frame covers at
        // speed — about as close to the ideal line as a player can be asked to hold — and a
        // trigger too tight for that is authored unplayable however correct the geometry
        // measuring it is.
        (GameSession session, _) = NewSession();
        await session.StartNewGame();
        double slop = Speed * Frame.TotalSeconds;
        Flight flight = FlightFor(session).Hold();

        foreach (QuestDefinition quest in _campaign.Quests)
        {
            QuestMarkers markers = MarkersFor(quest);
            Quest playing = session.Quests.Find(quest.Id)!;
            (double dx, double dy) = Sideways(markers.Start, markers.End, slop);

            flight.To(markers.Start.Offset(dx, dy));
            Assert.Equal(QuestState.Active, playing.State);

            flight.To(Beyond(markers.Start, markers.End, quest.End.Distance).Offset(dx, dy));
            Assert.Equal(QuestState.Completed, playing.State);
        }
    }

    /// <summary>
    /// A step of <paramref name="units"/> at right angles to the line from <paramref name="from"/>
    /// to <paramref name="to"/> — how far beside their ideal course a player ends up.
    /// </summary>
    static (double X, double Y) Sideways(Position from, Position to, double units)
    {
        double length = from.DistanceTo(to);

        // no line to be beside, so one direction is as good as another
        return length <= 0
            ? (units, 0)
            : (-(to.Y - from.Y) / length * units, (to.X - from.X) / length * units);
    }

    [Fact]
    public async Task AFinishedPlaythrough_LeavesNoQuestUnstartedOrInProgress()
    {
        // The campaign declares no rewards and no unlocks yet — a quest completing changes the
        // quest's state and writes the save, and that is the whole of the follow-on today. What
        // can be asserted is that the campaign is finishable: nothing is left that the player
        // reached the end of the content still holding.
        (GameSession session, _) = NewSession();
        await session.StartNewGame();

        FlyEveryQuest(session);

        Assert.Equal(_campaign.Quests.Count, session.Quests.Completed.Count());
        Assert.Empty(session.Quests.Active);
        Assert.DoesNotContain(session.Quests.Quests, quest => quest.State is QuestState.NotStarted);
    }

    [Fact]
    public async Task EveryQuestStillCompletes_WhenOneFrameSweepsStraightPastItsMarkers()
    {
        // The stalled frame is the case #8 exists for, and the point at which a quest stops being
        // playable if triggers are sampled rather than swept. One frame carries the player from
        // clear of the field to clear of the far side of it, so neither end of the only frame
        // there is lands inside either trigger — the quest is played by the ground the frame
        // covered, or it is not played at all.
        (GameSession session, _) = NewSession();
        await session.StartNewGame();

        foreach (QuestDefinition quest in _campaign.Quests)
        {
            QuestMarkers markers = MarkersFor(quest);
            if (markers.Start == markers.End)
            {
                // a quest finished where it begins has no field to fly across, so there is
                // nothing here to sweep past
                continue;
            }

            double clearance = Math.Max(quest.Start.Distance, quest.End.Distance) * 4;
            Position before = Beyond(markers.End, markers.Start, clearance);
            Position after = Beyond(markers.Start, markers.End, clearance);

            session.Player.MoveTo(before);
            Flight flight = FlightFor(session, TimeSpan.FromSeconds(before.DistanceTo(after) / Speed));
            flight.To(after);

            Assert.Equal(1, flight.Frames);
            Assert.True(markers.Start.DistanceTo(before) > quest.Start.Distance, "the frame began inside the start trigger");
            Assert.True(markers.Start.DistanceTo(after) > quest.Start.Distance, "the frame ended inside the start trigger");
            Assert.True(markers.End.DistanceTo(before) > quest.End.Distance, "the frame began inside the end trigger");
            Assert.True(markers.End.DistanceTo(after) > quest.End.Distance, "the frame ended inside the end trigger");
            Assert.Equal(QuestState.Completed, session.Quests.Find(quest.Id)!.State);
        }
    }

    /// <summary>
    /// The position <paramref name="units"/> further on than <paramref name="through"/>, along the
    /// line running from <paramref name="from"/> through it — the ground a player carries on over
    /// when they do not stop at a marker.
    /// </summary>
    static Position Beyond(Position from, Position through, double units)
    {
        double length = from.DistanceTo(through);
        if (length <= 0)
        {
            // no line to carry on along, so there is nowhere further on to be
            return through;
        }

        return new Position(
            through.X + ((through.X - from.X) / length * units),
            through.Y + ((through.Y - from.Y) / length * units));
    }

    [Fact]
    public async Task NoQuestBegins_ForAFlightThatKeepsClearOfEveryMarker()
    {
        // The control on all of the above: a playthrough that never goes near the content must
        // finish nothing, or the tests here would pass on a watcher that simply fired everything.
        (GameSession session, _) = NewSession();
        await session.StartNewGame();
        double clearance = _campaign.Quests.Max(quest => Math.Max(quest.Start.Distance, quest.End.Distance)) + 1;
        double farSide = _world.QuestMarkers.Max(markers => Math.Max(markers.Start.Y, markers.End.Y)) + 1;

        Flight flight = FlightFor(session);
        session.Player.MoveTo(new Position(clearance, -farSide));
        flight.To(new Position(clearance, farSide));

        Assert.All(
            session.Quests.Quests,
            quest => Assert.Equal(QuestState.NotStarted, quest.State));
    }

    // ---- does the playthrough survive the save ----

    [Fact]
    public async Task AFinishedPlaythrough_IsWrittenToTheSave()
    {
        (GameSession session, InMemorySaveProgressService save) = NewSession();
        await session.StartNewGame();

        FlyEveryQuest(session);
        await session.PendingSave;

        Assert.Null(session.SaveError);
        Assert.All(
            _campaign.Quests,
            quest => Assert.Contains(
                save.Saved!.Quests,
                progress => progress.QuestId == quest.Id && progress.State is QuestState.Completed));
    }

    [Fact]
    public async Task AFinishedPlaythrough_IsReadBackCompleted_AndIsNotPlayedAgain()
    {
        (GameSession first, InMemorySaveProgressService save) = NewSession();
        await first.StartNewGame();
        FlyEveryQuest(first);
        await first.PendingSave;

        GameSession resumed = new(save, _campaign, _world);
        List<string> replayed = [];
        resumed.Quests.QuestStarted += (_, e) => replayed.Add(e.Quest.Id);
        resumed.Quests.QuestCompleted += (_, e) => replayed.Add(e.Quest.Id);
        await resumed.Continue();

        Assert.Equal(_campaign.Quests.Count, resumed.Quests.Completed.Count());

        // and flying the whole campaign again does not start any of it over
        FlyEveryQuest(resumed);

        Assert.Empty(replayed);
        Assert.Equal(_campaign.Quests.Count, resumed.Quests.Completed.Count());
    }

    [Fact]
    public async Task AQuestAbandonedPartWayThrough_CanBeFinishedInALaterSession()
    {
        // The playthrough a player actually has: begin it, stop somewhere in the middle, come back
        // later. A quest that can only be completed in the session it began in is not playable.
        (GameSession first, InMemorySaveProgressService save) = NewSession();
        await first.StartNewGame();
        QuestDefinition quest = _campaign.Quests[0];
        QuestMarkers markers = MarkersFor(quest);

        FlightFor(first).Hold().To(Halfway(markers));
        await first.PendingSave;
        await first.Save();

        GameSession resumed = new(save, _campaign, _world);
        await resumed.Continue();

        Assert.Equal(QuestState.Active, resumed.Quests.Find(quest.Id)!.State);

        FlightFor(resumed).To(markers.End);
        await resumed.PendingSave;

        Assert.Equal(QuestState.Completed, resumed.Quests.Find(quest.Id)!.State);
        Assert.Contains(
            save.Saved!.Quests,
            progress => progress.QuestId == quest.Id && progress.State is QuestState.Completed);
    }

    static Position Halfway(QuestMarkers markers) => new(
        (markers.Start.X + markers.End.X) / 2,
        (markers.Start.Y + markers.End.Y) / 2);

    // ---- and in every language ----

    [Fact]
    public async Task TheWholePlaythrough_RunsTheSameInAnyLanguage()
    {
        // Quests are driven by ids and states, and only the title is translated. A playthrough
        // that depended on the player's language would be a quest that could not be finished in
        // one of them, and a save that could not cross between them.
        using CultureScope _ = new("fr");
        (GameSession session, InMemorySaveProgressService save) = NewSession();
        await session.StartNewGame();

        FlyEveryQuest(session);
        await session.PendingSave;

        Assert.Equal(_campaign.Quests.Count, session.Quests.Completed.Count());
        Assert.All(
            session.Quests.Quests,
            quest => Assert.Contains(
                save.Saved!.Quests,
                progress => progress.QuestId == quest.Id && progress.State is QuestState.Completed));

        // the run really was in French rather than English wearing a French label, and the id the
        // save is keyed on is not the translated thing
        Quest played = session.Quests.Find(BattleForceCampaign.Quest1Id)!;
        Assert.Equal("Sors de là. Le champ de débris s'effondre autour de toi.", played.Title);
        Assert.DoesNotContain(played.Title, save.Content);
    }

    [Fact]
    public async Task ASaveWrittenInOneLanguage_IsPlayedOnInAnother()
    {
        InMemorySaveProgressService save = new();

        using (CultureScope _ = new("ja"))
        {
            GameSession japanese = new(save, _campaign, _world);
            await japanese.StartNewGame();
            FlightFor(japanese).Hold().To(Halfway(MarkersFor(_campaign.Quests[0])));
            await japanese.Save();
        }

        using (CultureScope _ = new("de"))
        {
            GameSession german = new(save, _campaign, _world);
            await german.Continue();

            Assert.Equal(QuestState.Active, german.Quests.Find(_campaign.Quests[0].Id)!.State);

            FlightFor(german).To(MarkersFor(_campaign.Quests[0]).End);

            Assert.Equal(QuestState.Completed, german.Quests.Find(_campaign.Quests[0].Id)!.State);
        }
    }
}
