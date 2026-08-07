using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the ship's handling as game content: the physics is the engine's and is covered there,
/// but the numbers decide whether the game is flyable, and whether the quests it ships with can
/// actually be reached.
/// </summary>
public sealed class DisgracedShipTests
{
    static readonly ShipHandling Handling = DisgracedShip.Handling;

    static QuestDefinition Quest1 =>
        new BattleForceCampaign().Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

    static QuestMarkers Quest1Markers =>
        new BattleForceWorld().QuestMarkers.Single(marker => marker.QuestId == BattleForceCampaign.Quest1Id);

    [Fact]
    public void CanBeFlown()
    {
        // the same rules ShipMovement enforces, checked against the shipping numbers so a bad
        // edit to the content fails here rather than the first time the game is launched
        Assert.True(Handling.Acceleration > 0);
        Assert.True(Handling.Drag > 0);
        Assert.True(Handling.TurnRate > 0);
    }

    [Fact]
    public void HasATopSpeed()
    {
        Assert.Equal(200, Handling.MaximumSpeed, 6);
    }

    [Fact]
    public void TurnsRightRoundInUnderTwoSeconds()
    {
        // a ship that takes longer than this to come about makes every correction an argument
        Assert.True(Math.Tau / Handling.TurnRate < 2, "the ship is too slow to turn");
    }

    [Fact]
    public void ComesAbout_InALittleUnderASecondAndAHalf()
    {
        // The figure `DisgracedShip.Handling` documents, asserted against the number it ships, so
        // the two cannot drift apart again. They already had: the remark said a second and a half
        // while the rate said two and a half seconds, and neither noticed because nothing measured
        // the duration — only that it was under two. A test on the number alone would have to be
        // rewritten every time the helm is tuned; this one only fails when the ship stops handling
        // the way it is described as handling.
        Assert.InRange(Math.Tau / Handling.TurnRate, 1.3, 1.5);
    }

    [Fact]
    public void ReachesQuest1sExitMarker_AtFullThrust_InAPlayableTime()
    {
        Player player = new();
        ShipMovement ship = new(Handling, DisgracedShip.Profile.HullRadius);
        Position exit = Quest1Markers.End;

        int frames = 0;
        while (player.Position.DistanceTo(exit) > Quest1.End.Distance)
        {
            Assert.True(frames < 60 * 30, "the ship never got clear of the debris field");
            ship.Update(player, new ShipControls(thrust: 1, turn: 0), TimeSpan.FromMilliseconds(20));
            frames++;
        }

        // a run measured in seconds: long enough to be a flight, short enough to be an opening
        double seconds = frames * 0.02;
        Assert.InRange(seconds, 3, 15);
    }

    [Fact]
    public void AtFullSpeed_AFrameStaysWellInsideQuest1sTriggers()
    {
        // A tuning check rather than the safeguard it once was: the watcher now sweeps the ground
        // covered by the frame, so a marker fires whatever the frame length. What this holds is
        // that a marker is still something the ship spends a frame or more inside at full burn —
        // a trigger narrower than a frame's travel fires on a single frame's sweep and is gone,
        // which leaves nothing for a HUD or a sound to be shown against.
        double perFrameAt30Hz = Handling.MaximumSpeed / 30;

        Assert.True(
            perFrameAt30Hz < Quest1.Start.Distance,
            $"{perFrameAt30Hz} units a frame can step over a {Quest1.Start.Distance} unit start trigger");
        Assert.True(
            perFrameAt30Hz < Quest1.End.Distance,
            $"{perFrameAt30Hz} units a frame can step over a {Quest1.End.Distance} unit end trigger");
    }
}
