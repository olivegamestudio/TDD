namespace OliveGameStudio.World.Tests;

public sealed class NeutralShipInputTests
{
    [Fact]
    public void AsksForNothing_SoAnUnpilotedShipHoldsStill()
    {
        IShipInput input = new NeutralShipInput();

        Assert.Equal(ShipControls.Neutral, input.Read());
    }

    [Fact]
    public void ReadsTheSameOnEveryFrame()
    {
        IShipInput input = new NeutralShipInput();

        Assert.Equal(input.Read(), input.Read());
    }
}
