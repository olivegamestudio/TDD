namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers what a key press means to the ship. A key is digital and the ship is analogue, so the
/// translation is the part worth pinning — in particular what two opposite keys held at once
/// come to, which is the case a player produces by accident within a minute of playing.
/// </summary>
public sealed class ShipControlsFromKeysTests
{
    [Fact]
    public void NoKeys_AreHandsOff()
    {
        ShipControls controls = ShipControls.FromKeys(ahead: false, astern: false, port: false, starboard: false);

        Assert.Equal(0, controls.Thrust);
        Assert.Equal(0, controls.Turn);
        Assert.True(controls.IsNeutral);
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, true, -1)]
    public void AThrustKey_AsksForTheFullRange(bool ahead, bool astern, double expected)
    {
        // a key has no travel, so holding it is asking for everything the ship has
        ShipControls controls = ShipControls.FromKeys(ahead, astern, port: false, starboard: false);

        Assert.Equal(expected, controls.Thrust);
    }

    [Theory]
    [InlineData(true, false, -1)]
    [InlineData(false, true, 1)]
    public void AHelmKey_AsksForTheFullRange(bool port, bool starboard, double expected)
    {
        ShipControls controls = ShipControls.FromKeys(ahead: false, astern: false, port: port, starboard: starboard);

        Assert.Equal(expected, controls.Turn);
    }

    [Fact]
    public void OppositeThrustKeys_Cancel()
    {
        // and the answer must not depend on which key was looked at first, which is why the two
        // are summed rather than tested in order
        ShipControls controls = ShipControls.FromKeys(ahead: true, astern: true, port: false, starboard: false);

        Assert.Equal(0, controls.Thrust);
    }

    [Fact]
    public void OppositeHelmKeys_Cancel()
    {
        ShipControls controls = ShipControls.FromKeys(ahead: false, astern: false, port: true, starboard: true);

        Assert.Equal(0, controls.Turn);
    }

    [Fact]
    public void AHandFlatOnEveryKey_IsHandsOff_AndSaysSo()
    {
        // a flat hand cancels on both axes; it must also report itself as nobody flying, or it
        // would win the arbitration and shut the other device out
        ShipControls controls = ShipControls.FromKeys(ahead: true, astern: true, port: true, starboard: true);

        Assert.True(controls.IsNeutral);
    }

    [Fact]
    public void TheAxesAreIndependent()
    {
        ShipControls controls = ShipControls.FromKeys(ahead: true, astern: false, port: true, starboard: false);

        Assert.Equal(1, controls.Thrust);
        Assert.Equal(-1, controls.Turn);
    }

    [Theory]
    [InlineData(true, false, -1)]
    [InlineData(false, true, 1)]
    public void AStrafeKey_AsksForTheFullRange(bool strafePort, bool strafeStarboard, double expected)
    {
        ShipControls controls = ShipControls.FromKeys(
            ahead: false, astern: false, port: false, starboard: false, strafePort, strafeStarboard);

        Assert.Equal(expected, controls.Strafe);
    }

    [Fact]
    public void OppositeStrafeKeys_Cancel()
    {
        ShipControls controls = ShipControls.FromKeys(
            ahead: false, astern: false, port: false, starboard: false, strafePort: true, strafeStarboard: true);

        Assert.Equal(0, controls.Strafe);
    }

    [Fact]
    public void NoStrafeKeysGiven_DefaultsToHandsOff()
    {
        // every call site written before strafing existed still asks for exactly what it did
        // before, because the two new keys default to not held
        ShipControls controls = ShipControls.FromKeys(ahead: false, astern: false, port: false, starboard: false);

        Assert.Equal(0, controls.Strafe);
    }

    [Fact]
    public void StrafeIsIndependent_OfThrustAndTurn()
    {
        ShipControls controls = ShipControls.FromKeys(
            ahead: true, astern: false, port: true, starboard: false, strafePort: false, strafeStarboard: true);

        Assert.Equal(1, controls.Thrust);
        Assert.Equal(-1, controls.Turn);
        Assert.Equal(1, controls.Strafe);
    }
}
