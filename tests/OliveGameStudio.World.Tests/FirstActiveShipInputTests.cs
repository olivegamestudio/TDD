namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers two devices bound at once: the one being used answers, and their answers are never
/// added together.
/// </summary>
public sealed class FirstActiveShipInputTests
{
    sealed class FakeDevice(ShipControls controls) : IShipInput
    {
        public ShipControls Controls { get; set; } = controls;

        public int Reads { get; private set; }

        public ShipControls Read()
        {
            Reads++;
            return Controls;
        }
    }

    static FakeDevice Resting() => new(ShipControls.Neutral);

    [Fact]
    public void WithNoDevices_AsksForNothing()
    {
        // the same answer as nobody at the controls, rather than a crash on an empty list
        FirstActiveShipInput input = new();

        Assert.True(input.Read().IsNeutral);
    }

    [Fact]
    public void EveryDeviceResting_AsksForNothing()
    {
        FirstActiveShipInput input = new(Resting(), Resting());

        Assert.True(input.Read().IsNeutral);
    }

    [Fact]
    public void ReportsTheOnlyDeviceBeingUsed()
    {
        FakeDevice keyboard = new(new ShipControls(thrust: 1, turn: 0));
        FirstActiveShipInput input = new(Resting(), keyboard);

        Assert.Equal(new ShipControls(1, 0), input.Read());
    }

    [Fact]
    public void WithBothDevicesInUse_TheFirstWinsOutright_RatherThanTheTwoSumming()
    {
        // a stick half over plus a held key must not read as more than full thrust
        FakeDevice pad = new(new ShipControls(thrust: 0.5, turn: 0));
        FakeDevice keyboard = new(new ShipControls(thrust: 1, turn: 1));
        FirstActiveShipInput input = new(pad, keyboard);

        Assert.Equal(new ShipControls(0.5, 0), input.Read());
    }

    [Fact]
    public void TheWinningDeviceAnswersForBothAxes()
    {
        // per device, not per axis: a stick supplying the helm while a key supplied the thrust is
        // the summing this exists to avoid, only harder to predict
        FakeDevice pad = new(new ShipControls(thrust: 0, turn: 0.5));
        FakeDevice keyboard = new(new ShipControls(thrust: 1, turn: 0));
        FirstActiveShipInput input = new(pad, keyboard);

        Assert.Equal(new ShipControls(0, 0.5), input.Read());
    }

    [Fact]
    public void LettingGoOfOneDeviceHandsControlStraightToTheOther()
    {
        // stateless: no memory of which device was used last, so there is no delay the player
        // cannot see
        FakeDevice pad = new(new ShipControls(thrust: 0.5, turn: 0));
        FakeDevice keyboard = new(new ShipControls(thrust: 1, turn: 0));
        FirstActiveShipInput input = new(pad, keyboard);
        Assert.Equal(new ShipControls(0.5, 0), input.Read());

        pad.Controls = ShipControls.Neutral;

        Assert.Equal(new ShipControls(1, 0), input.Read());
    }

    [Fact]
    public void DoesNotAskALaterDeviceOnceOneHasAnswered()
    {
        // read once a frame on the frame path; a device that has already lost is not polled
        FakeDevice pad = new(new ShipControls(thrust: 0.5, turn: 0));
        FakeDevice keyboard = Resting();
        FirstActiveShipInput input = new(pad, keyboard);

        input.Read();

        Assert.Equal(1, pad.Reads);
        Assert.Equal(0, keyboard.Reads);
    }
}
