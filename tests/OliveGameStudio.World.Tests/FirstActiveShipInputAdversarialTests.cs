namespace OliveGameStudio.World.Tests;

/// <summary>
/// QA's adversarial pass over the arbitration. The shipping composition binds exactly two devices
/// in a fixed order, so what is probed here is the behaviour either side of that: a device that
/// answers with something the mapping could have produced from a hostile driver, devices that
/// change their minds between frames, and the shapes of device list a second host could bind.
/// </summary>
public sealed class FirstActiveShipInputAdversarialTests
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
    public void ADeviceHandedEveryHostileNumber_NeverShutsOutTheDeviceThatWorks()
    {
        // the failure this guards is the worst one to diagnose: the player's hands are on the
        // controls, the ship does not move, and nothing is logged. It happens the moment a broken
        // device can claim to be the one in use
        foreach (double hostile in new[]
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue,
            double.MinValue, double.Epsilon, -0.0, 0,
        })
        {
            foreach (double deadZone in new[] { 0.2, double.NaN, 1, 2, 0 })
            {
                ShipControls broken = ShipControls.FromStick(hostile, hostile, deadZone);
                FirstActiveShipInput input = new(
                    new FakeDevice(broken),
                    new FakeDevice(ShipControls.FromKeys(true, false, false, false)));

                ShipControls answer = input.Read();

                // either the broken device genuinely asked for something, or the keyboard flies
                Assert.True(
                    !broken.IsNeutral || answer.Thrust == 1,
                    $"a device reading ({hostile}, dead zone {deadZone}) left the ship on {answer.Thrust}");
                Assert.False(double.IsNaN(answer.Thrust), $"NaN reached the physics from {hostile}");
            }
        }
    }

    [Fact]
    public void ADeviceAskingForTheFaintestThingItCan_StillWins()
    {
        // the arbitration threshold is "anything at all", so the smallest representable ask has to
        // count — otherwise there is a band where a device is in use and is ignored
        FakeDevice faint = new(new ShipControls(double.Epsilon, 0));
        FirstActiveShipInput input = new(faint, new FakeDevice(new ShipControls(1, 1)));

        Assert.Equal(double.Epsilon, input.Read().Thrust);
    }

    [Fact]
    public void ChangingHandsEveryFrame_TheAnswerFollowsImmediately()
    {
        // stateless on purpose: nothing may carry over from the previous frame, or a player
        // swapping devices gets a delay they cannot see the reason for
        FakeDevice pad = Resting();
        FakeDevice keys = Resting();
        FirstActiveShipInput input = new(pad, keys);

        for (int frame = 0; frame < 10; frame++)
        {
            bool padsTurn = frame % 2 == 0;
            pad.Controls = padsTurn ? new ShipControls(1, 0) : ShipControls.Neutral;
            keys.Controls = padsTurn ? ShipControls.Neutral : new ShipControls(-1, 0);

            Assert.Equal(padsTurn ? 1 : -1, input.Read().Thrust);
        }
    }

    [Fact]
    public void ReadingItTwiceInAFrame_AsksTheDevicesAgain_RatherThanCachingAStaleFrame()
    {
        // it holds no state, so it must not appear to: a cached answer would go stale silently
        FakeDevice pad = Resting();
        FirstActiveShipInput input = new(pad);

        input.Read();
        pad.Controls = new ShipControls(1, 0);

        Assert.Equal(1, input.Read().Thrust);
        Assert.Equal(2, pad.Reads);
    }

    [Fact]
    public void TheSameDeviceBoundTwice_IsNotAskedTwiceOnceItHasAnswered()
    {
        // a composition root that binds a device twice by mistake must not double its polling
        FakeDevice pad = new(new ShipControls(1, 0));
        FirstActiveShipInput input = new(pad, pad);

        Assert.Equal(1, input.Read().Thrust);
        Assert.Equal(1, pad.Reads);
    }

    [Fact]
    public void ADeviceLateInAListOfMany_StillFliesTheShip()
    {
        // nothing caps how many devices a host may bind, and the last one must not be unreachable
        List<IShipInput> devices = [];
        for (int resting = 0; resting < 32; resting++)
        {
            devices.Add(Resting());
        }

        FakeDevice inUse = new(new ShipControls(1, -1));
        devices.Add(inUse);
        FirstActiveShipInput input = new([.. devices]);

        Assert.Equal(1, input.Read().Thrust);
        Assert.Equal(-1, input.Read().Turn);
    }

    [Fact]
    public void EveryDeviceResting_TheAnswerIsExactlyNeutral_NotMerelyEquivalent()
    {
        // GameScreen hands this straight to the physics; a "nearly zero" would integrate into a
        // drift over a long session
        FirstActiveShipInput input = new(Resting(), Resting(), Resting());

        ShipControls answer = input.Read();

        Assert.Equal(ShipControls.Neutral, answer);
        Assert.Equal(0, answer.Thrust);
        Assert.Equal(0, answer.Turn);
    }

    [Fact]
    public void AnEmptyListReadRepeatedly_StaysNeutral_AndNeverThrows()
    {
        // the engine's own default is neutral, so an empty bind has to behave like no pilot rather
        // than like a fault
        FirstActiveShipInput input = new();

        for (int frame = 0; frame < 100; frame++)
        {
            Assert.True(input.Read().IsNeutral);
        }
    }

    [Fact]
    public void TheWinningDevicesAskIsPassedThroughUntouched()
    {
        // arbitration picks a device; it does not get a vote on what that device asked for
        ShipControls asked = ShipControls.FromStick(0.6, -0.35, 0.2);
        FirstActiveShipInput input = new(Resting(), new FakeDevice(asked), new FakeDevice(new ShipControls(1, 1)));

        Assert.Equal(asked, input.Read());
    }
}
