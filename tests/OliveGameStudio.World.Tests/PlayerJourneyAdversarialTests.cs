namespace OliveGameStudio.World.Tests;

/// <summary>
/// QA's adversarial cover for the journey the player hands over (#8). The sweep is only as good as
/// the ground it is given, so these go after the handover rather than the geometry: ground counted
/// twice, ground lost between frames, and the difference between having flown somewhere and having
/// been put there.
/// </summary>
public sealed class PlayerJourneyAdversarialTests
{
    [Fact]
    public void AFrameThatMovesThePlayerInSeveralSteps_IsOneJourney()
    {
        // Nothing says a frame moves the player exactly once. If each step overwrote the last, the
        // swept segment would be the final step only and everything before it would be unswept
        // ground — the very defect #8 exists to close, reintroduced one layer up.
        Player player = new();

        player.MoveBy(0, 100);
        player.MoveBy(0, 100);
        player.MoveBy(0, 100);

        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 0), from);
        Assert.Equal(new Position(0, 300), to);
    }

    [Fact]
    public void TakingTheJourneyTwice_CoversNoGroundTheSecondTime()
    {
        // Ground swept twice is a trigger that could fire on a frame the player never flew.
        Player player = new();
        player.MoveBy(0, 500);

        player.TakeJourney();
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 500), from);
        Assert.Equal(new Position(0, 500), to);
    }

    [Fact]
    public void TheNextJourneyBeginsWhereTheLastEnded_SoNoGroundIsMissedBetweenFrames()
    {
        Player player = new();
        player.MoveBy(0, 500);
        player.TakeJourney();

        player.MoveBy(0, 500);
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 500), from);
        Assert.Equal(new Position(0, 1000), to);
    }

    [Fact]
    public void PlacingThePlayerEndsTheJourney_RatherThanExtendingIt()
    {
        // Resuming a save at (0, 700) must not sweep the line from the origin to it, running the
        // player through every marker in between on a journey nobody made.
        Player player = new();

        player.MoveTo(new Position(0, 700));
        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 700), from);
        Assert.Equal(new Position(0, 700), to);
    }

    [Fact]
    public void PlacingThePlayerAfterFlying_DiscardsTheGroundFlownBeforeThePlacement()
    {
        // A placement in the middle of a frame ends the journey, so the ground flown before it is
        // never swept. Pinned because it is a stated rule with a cost, not an oversight: the
        // alternative is a swept line from where the player was flying to wherever they were put.
        Player player = new();

        player.MoveBy(0, 500);
        player.MoveTo(new Position(0, 2000));

        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(new Position(0, 2000), from);
        Assert.Equal(new Position(0, 2000), to);
    }

    [Fact]
    public void APlayerWhoHasNeverMoved_OffersAJourneyThatWentNowhere()
    {
        Player player = new();

        (Position from, Position to) = player.TakeJourney();

        Assert.Equal(Position.Origin, from);
        Assert.Equal(Position.Origin, to);
    }
}
