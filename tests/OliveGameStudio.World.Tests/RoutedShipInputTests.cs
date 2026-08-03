namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers the seam between input that is pushed and physics that pulls.
/// </summary>
public sealed class RoutedShipInputTests
{
    [Fact]
    public void BeforeAnythingRoutes_NobodyIsFlying()
    {
        // the ship must be still before the first frame arrives, not moving on whatever a
        // default-constructed value happened to be
        Assert.True(new RoutedShipInput().Read().IsNeutral);
    }

    [Fact]
    public void ItReportsWhatWasLastPutOnIt()
    {
        RoutedShipInput input = new() { Controls = new ShipControls(1, -1) };

        Assert.Equal(new ShipControls(1, -1), input.Read());
    }

    [Fact]
    public void ReadingDoesNotConsume()
    {
        // a frame that is updated or drawn twice has to see the same controls both times, so a
        // read leaves the value where it is
        RoutedShipInput input = new() { Controls = new ShipControls(1, 0) };

        input.Read();

        Assert.Equal(new ShipControls(1, 0), input.Read());
    }

    [Fact]
    public void TheLastValueWins()
    {
        RoutedShipInput input = new() { Controls = new ShipControls(1, 0) };

        input.Controls = ShipControls.Neutral;

        Assert.True(input.Read().IsNeutral);
    }
}
