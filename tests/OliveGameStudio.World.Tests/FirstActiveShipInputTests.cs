namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers which device answers when more than one is bound. The rule is per device rather than
/// per axis, so the tests that matter most are the ones where the two devices disagree.
/// </summary>
public sealed class FirstActiveShipInputTests
{
    sealed class Fixed(ShipControls controls) : IShipInput
    {
        public int Reads { get; private set; }

        public ShipControls Read()
        {
            Reads++;
            return controls;
        }
    }

    static Fixed Resting => new(ShipControls.Neutral);

    [Fact]
    public void NoDevices_AreHandsOff()
    {
        Assert.True(new FirstActiveShipInput().Read().IsNeutral);
    }

    [Fact]
    public void EveryDeviceResting_IsHandsOff()
    {
        Assert.True(new FirstActiveShipInput(Resting, Resting).Read().IsNeutral);
    }

    [Fact]
    public void TheFirstDeviceAskingForSomething_Wins()
    {
        FirstActiveShipInput input = new(
            Resting,
            new Fixed(new ShipControls(thrust: 1, turn: 0)),
            new Fixed(new ShipControls(thrust: -1, turn: 0)));

        Assert.Equal(1, input.Read().Thrust);
    }

    [Fact]
    public void TheWinningDeviceTakesBothAxes_RatherThanTheTwoSumming()
    {
        // per device, not per axis. Letting a stick supply the helm while a key supplies the
        // throttle is the summing the ticket rules out, only harder for a player to predict —
        // and it is stateless this way, so letting go of one device hands over on the next frame
        FirstActiveShipInput input = new(
            new Fixed(new ShipControls(thrust: 0, turn: 1)),
            new Fixed(new ShipControls(thrust: 1, turn: 0)));

        ShipControls controls = input.Read();

        Assert.Equal(0, controls.Thrust);
        Assert.Equal(1, controls.Turn);
    }

    [Fact]
    public void ADeviceBehindTheWinner_IsNotEvenAsked()
    {
        // reading a device can cost something, and asking one that cannot win is work for nothing
        Fixed behind = new(new ShipControls(thrust: 1, turn: 0));
        FirstActiveShipInput input = new(new Fixed(new ShipControls(thrust: -1, turn: 0)), behind);

        input.Read();

        Assert.Equal(0, behind.Reads);
    }

    [Fact]
    public void ADeviceReportingNothingReadable_DoesNotWinTheArbitration()
    {
        // an unreadable axis is normalised to nothing at construction, so it reports itself as
        // resting rather than claiming to be the device in use. Pinned here as well as on
        // ShipControls, because this is where a player would feel it: the pad is asked first, so
        // a pad whose driver answers NaN would otherwise lock the keyboard out entirely
        FirstActiveShipInput input = new(
            new Fixed(new ShipControls(double.NaN, double.NaN)),
            new Fixed(new ShipControls(thrust: 1, turn: 0)));

        Assert.Equal(1, input.Read().Thrust);
    }

    [Fact]
    public void ItCachesNothing_SoLettingGoHandsOverOnTheNextFrame()
    {
        ChangingInput first = new() { Controls = new ShipControls(thrust: 1, turn: 0) };
        FirstActiveShipInput input = new(first, new Fixed(new ShipControls(thrust: 0, turn: 1)));

        Assert.Equal(1, input.Read().Thrust);

        first.Controls = ShipControls.Neutral;

        Assert.Equal(1, input.Read().Turn);
    }

    sealed class ChangingInput : IShipInput
    {
        public ShipControls Controls { get; set; } = ShipControls.Neutral;

        public ShipControls Read() => Controls;
    }

    [Fact]
    public void ADeviceThatIsNotThere_IsRefusedWhereItIsBound()
    {
        // refused at composition rather than on the frame path, where the only thing a null
        // device could do is take the game down mid-flight
        Assert.Throws<ArgumentException>(() => new FirstActiveShipInput(Resting, null!));
    }

    [Fact]
    public void TheDevicesCannotBeChangedBehindItsBack()
    {
        IShipInput[] bound = [new Fixed(new ShipControls(thrust: 1, turn: 0))];
        FirstActiveShipInput input = new(bound);

        bound[0] = Resting;

        Assert.Equal(1, input.Read().Thrust);
    }
}
