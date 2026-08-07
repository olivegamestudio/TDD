namespace OliveGameStudio.Input.Tests;

/// <summary>
/// Covers what fingers on the two overlay circles ask the ship for.
/// </summary>
/// <remarks>
/// The circles are placed where a mobile HUD would put them — one low left, one low right — but
/// nothing here depends on those numbers being the shipped ones. What is being pinned is the
/// relationship between where a finger is and what the ship is asked to do, which holds wherever
/// the circles end up.
/// </remarks>
public sealed class TouchOverlayTests
{
    static readonly TouchCircle Helm = new(200, 600, 100);
    static readonly TouchCircle Fire = new(1000, 600, 100);

    readonly TouchOverlay _overlay = new(Helm, Fire);

    static TouchFrame Touches(params TouchPoint[] points) => new(points);

    /// <summary>
    /// A finger on the helm circle, offset from its centre by the given pixels.
    /// </summary>
    static TouchPoint OnHelm(double dx, double dy, int id = 1) =>
        new(id, Helm.CentreX + dx, Helm.CentreY + dy);

    static TouchPoint OnFire(double dx = 0, double dy = 0, int id = 2) =>
        new(id, Fire.CentreX + dx, Fire.CentreY + dy);

    [Fact]
    public void NobodyTouchingTheScreenFliesNothing()
    {
        TouchControls controls = _overlay.Read(TouchFrame.None);

        Assert.Equal(ShipControls.Neutral, controls.Ship);
        Assert.False(controls.Firing);
    }

    /// <summary>
    /// A frame that was never given a list at all — <c>default</c> rather than an empty one — is
    /// the same as nobody touching anything. A host that reports its panel this way must not be
    /// able to take the game down on the frame path.
    /// </summary>
    [Fact]
    public void AFrameThatCarriesNoListAtAllFliesNothing()
    {
        TouchControls controls = _overlay.Read(default);

        Assert.Equal(ShipControls.Neutral, controls.Ship);
        Assert.False(controls.Firing);
    }

    /// <summary>
    /// A thumb resting dead centre is asking for nothing. There is no dead zone here and none is
    /// needed: a finger is where it is put, and unlike a stick it does not fail to come back.
    /// </summary>
    [Fact]
    public void AThumbAtTheCentreAsksForNothing()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, 0)));

        Assert.Equal(ShipControls.Neutral, controls.Ship);
    }

    [Fact]
    public void AThumbAtTheTopOfTheCircleAsksForFullAhead()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -100)));

        Assert.Equal(1, controls.Ship.Thrust);
        Assert.Equal(0, controls.Ship.Turn);
    }

    [Fact]
    public void AThumbAtTheBottomOfTheCircleAsksForFullAstern()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, 100)));

        Assert.Equal(-1, controls.Ship.Thrust);
        Assert.Equal(0, controls.Ship.Turn);
    }

    [Fact]
    public void AThumbToTheRightTurnsToStarboard()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(100, 0)));

        Assert.Equal(1, controls.Ship.Turn);
        Assert.Equal(0, controls.Ship.Thrust);
    }

    [Fact]
    public void AThumbToTheLeftTurnsToPort()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(-100, 0)));

        Assert.Equal(-1, controls.Ship.Turn);
        Assert.Equal(0, controls.Ship.Thrust);
    }

    /// <summary>
    /// The circle is a stick, not a switch: half way out is half the engine. Without this the
    /// overlay would be four digital keys wearing a circle, and a player could not ease into a
    /// turn.
    /// </summary>
    [Fact]
    public void HalfWayOutIsHalfTheEngine()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -50)));

        Assert.Equal(0.5, controls.Ship.Thrust, 10);
    }

    /// <summary>
    /// A finger put down outside the circle is not flying: the overlay is two circles, not the
    /// whole screen, and the rest of the screen has other things on it.
    /// </summary>
    [Fact]
    public void AFingerPutDownOutsideTheCircleDoesNotFly()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -101)));

        Assert.Equal(ShipControls.Neutral, controls.Ship);
    }

    /// <summary>
    /// Once the circle has a thumb it keeps it, wherever that thumb goes. A player pushing for
    /// full ahead slides off the circle without noticing, and a stick that let go there would
    /// cut the engine at exactly the moment they asked for all of it.
    /// </summary>
    [Fact]
    public void AThumbThatSlidesOffTheCircleKeepsSteering()
    {
        _overlay.Read(Touches(OnHelm(0, -50)));

        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -400)));

        Assert.Equal(1, controls.Ship.Thrust);
    }

    /// <summary>
    /// Dragged past the edge diagonally, the stick is at its stop rather than past it: the
    /// deflection is scaled back to the rim, so a thumb halfway across the screen asks for full
    /// ahead and full starboard rather than for several times the engine.
    /// </summary>
    [Fact]
    public void AThumbDraggedWellPastTheRimIsHeldAtFullDeflection()
    {
        _overlay.Read(Touches(OnHelm(0, 0)));

        TouchControls controls = _overlay.Read(Touches(OnHelm(500, -500)));

        double thrust = controls.Ship.Thrust;
        double turn = controls.Ship.Turn;

        Assert.Equal(1, (thrust * thrust) + (turn * turn), 10);
        Assert.True(thrust > 0 && turn > 0);
    }

    /// <summary>
    /// The thumb comes off and the ship stops being flown. Nothing else clears the controls, so a
    /// lifted finger that kept its last deflection would be a ship that flies on by itself.
    /// </summary>
    [Fact]
    public void LiftingTheThumbHandsBackTheControls()
    {
        _overlay.Read(Touches(OnHelm(0, -100)));

        TouchControls controls = _overlay.Read(TouchFrame.None);

        Assert.Equal(ShipControls.Neutral, controls.Ship);
    }

    /// <summary>
    /// A second finger landing on the circle does not take it. The player is holding a heading
    /// with one thumb; a palm or a second hand touching down must not snatch the helm away
    /// mid-turn.
    /// </summary>
    [Fact]
    public void ASecondFingerOnTheCircleDoesNotTakeTheHelm()
    {
        _overlay.Read(Touches(OnHelm(0, -100, id: 1)));

        // the newcomer reported first, so a helm that simply took the first finger on the circle
        // would answer with theirs rather than with the one that has been holding it
        TouchControls controls = _overlay.Read(
            Touches(OnHelm(0, 100, id: 7), OnHelm(0, -100, id: 1)));

        Assert.Equal(1, controls.Ship.Thrust);
    }

    /// <summary>
    /// The helm is free again once the finger holding it lifts, and the next finger down takes it.
    /// A helm still owned by a finger that has gone would be a circle that stops working.
    /// </summary>
    [Fact]
    public void TheHelmIsFreeAgainOnceTheThumbHoldingItLifts()
    {
        _overlay.Read(Touches(OnHelm(0, -100, id: 1)));
        _overlay.Read(TouchFrame.None);

        TouchControls controls = _overlay.Read(Touches(OnHelm(0, 100, id: 2)));

        Assert.Equal(-1, controls.Ship.Thrust);
    }

    [Fact]
    public void AFingerOnTheRightCircleFires()
    {
        TouchControls controls = _overlay.Read(Touches(OnFire()));

        Assert.True(controls.Firing);
        Assert.Equal(ShipControls.Neutral, controls.Ship);
    }

    /// <summary>
    /// Firing is a button and is held rather than captured: sliding a finger off it stops the
    /// firing, which is the same thing letting go of the button does. Only the helm follows a
    /// finger past its rim, because only the helm is a stick.
    /// </summary>
    [Fact]
    public void SlidingOffTheFireCircleStopsFiring()
    {
        _overlay.Read(Touches(OnFire()));

        TouchControls controls = _overlay.Read(Touches(OnFire(dx: 400)));

        Assert.False(controls.Firing);
    }

    [Fact]
    public void FlyingAndFiringAtOnceIsBothAtOnce()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -100), OnFire()));

        Assert.Equal(1, controls.Ship.Thrust);
        Assert.True(controls.Firing);
    }

    [Fact]
    public void TheHelmCircleDoesNotFire()
    {
        TouchControls controls = _overlay.Read(Touches(OnHelm(0, -100)));

        Assert.False(controls.Firing);
    }

    /// <summary>
    /// A touch the host could not place is not a touch at the origin, and it is not a turn to
    /// port either. An axis that could not be read has already put a <c>NaN</c> through the
    /// heading once (<c>ShipControls.Axis</c>); a circle that answered <c>NaN</c> here would do
    /// it again from a different direction.
    /// </summary>
    [Fact]
    public void ATouchTheHostCouldNotPlaceFliesNothing()
    {
        TouchControls controls = _overlay.Read(
            Touches(new TouchPoint(1, double.NaN, double.NaN)));

        Assert.Equal(ShipControls.Neutral, controls.Ship);
        Assert.False(controls.Firing);
    }

    /// <summary>
    /// A circle with no radius has not been placed, and is not a circle the player can hit exactly
    /// once at its centre. Dividing the deflection by it would ask for infinite helm from a single
    /// pixel.
    /// </summary>
    [Fact]
    public void ACircleWithNoRadiusCannotBeTouched()
    {
        TouchOverlay unplaced = new(new TouchCircle(200, 600, 0), new TouchCircle(1000, 600, 0));

        TouchControls controls = unplaced.Read(
            Touches(new TouchPoint(1, 200, 600), new TouchPoint(2, 1000, 600)));

        Assert.Equal(ShipControls.Neutral, controls.Ship);
        Assert.False(controls.Firing);
    }

    /// <summary>
    /// Reading the same frame twice gives the same answer. The overlay remembers which finger holds
    /// the helm, and a frame that is read again — by a fixed-step update running twice — must not
    /// be able to change what that memory says.
    /// </summary>
    [Fact]
    public void ReadingTheSameFrameTwiceAsksForTheSameThing()
    {
        TouchFrame frame = Touches(OnHelm(0, -100), OnFire());

        TouchControls first = _overlay.Read(frame);
        TouchControls second = _overlay.Read(frame);

        Assert.Equal(first, second);
    }
}
