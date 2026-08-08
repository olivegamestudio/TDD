using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game screen as the seam between the screen lifecycle and gameplay: entering the
/// screen begins the game, each frame flies the ship and drives the quests from where the player
/// is, and what the frame produced is handed to the drawing side as a pose.
/// </summary>
public sealed class GameScreenTests : HostTestBase
{
    sealed class StubShipView : IShipView
    {
        public int Rendered { get; private set; }

        public string AssetKey { get; set; } = ShipView.DefaultAssetKey;

        public ShipPose Pose { get; set; }

        public float Thrust { get; set; }

        public void Render(IRenderer renderer) => Rendered++;
    }

    /// <summary>
    /// A session that is ready the moment it is built, so a screen assembled by hand can be
    /// driven a frame at a time without a save being read first. The drawing tests care about
    /// what reaches the view, not about where the game came from.
    /// </summary>
    sealed class StubGameSession : IGameSession
    {
        public StubGameSession()
        {
            // the same provisioning the real session does, so a hand assembled screen flies a ship
            // that came from a character rather than one the test invented
            Character = new Character(new TestRoster().Starting, Quests);
            Ship = new Ship(Character.Template.Ship);
            Character.Board(Ship);
        }

        public Player Player { get; } = new();

        public Character Character { get; }

        public Ship Ship { get; }

        public QuestLog Quests { get; } = new();

        public bool IsReady { get; set; } = true;

        public Task PendingSave => Task.CompletedTask;

        public Exception? SaveError => null;

        public bool IsSavingProgress => true;

        public Task StartNewGame() => Task.CompletedTask;

        public Task Continue() => Task.CompletedTask;

        public Task Save() => Task.CompletedTask;
    }

    /// <summary>
    /// Assembles a game screen by hand for the tests that are about drawing rather than about the
    /// path a player takes to get here. The gameplay dependencies are real but quiet: nobody at
    /// the controls, and a session that has nothing saved behind it.
    /// </summary>
    static GameScreen ScreenFor(
        ICamera camera,
        IShipView view,
        IGameSession? session = null,
        IShipInput? pilot = null,
        RegionView? region = null,
        HelpArrowView? helpArrows = null,
        CollisionDebugView? collisionDebug = null) =>
        new(session ?? new StubGameSession(),
            pilot ?? new NeutralShipInput(),
            new QuestProximityWatcher(new BattleForceWorld()),
            camera,
            view,
            new StarField(camera),
            region ?? new RegionView(camera),
            new RegionLoader(Path.Combine(AppContext.BaseDirectory, RegionLoader.FolderName)),
            new Vignette(),
            helpArrows ?? new HelpArrowView(camera),
            // Disabled unless a test is specifically about it: these tests assert on draw order
            // and draw count, and a developer overlay defaulting to drawn would be extra sprites
            // neither was written expecting.
            collisionDebug ?? new CollisionDebugView(camera) { Enabled = false });

    [Fact]
    public void Render_DrawsTheShip()
    {
        StubShipView ship = new();
        GameScreen screen = ScreenFor(new Camera2D(), ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(1, ship.Rendered);
    }

    [Fact]
    public void Render_PointsTheCameraAtTheShip()
    {
        // the camera follows the ship: a fixed viewport is left behind within seconds
        Camera2D camera = new();
        StubShipView ship = new() { Pose = new ShipPose(new Vector2(120f, -45f), 0f) };
        GameScreen screen = ScreenFor(camera, ship);

        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(120f, -45f), camera.Target);
    }

    [Fact]
    public void Render_KeepsTheCameraOnTheShip_AsItMoves()
    {
        Camera2D camera = new();
        StubShipView ship = new();
        GameScreen screen = ScreenFor(camera, ship);

        ship.Pose = new ShipPose(new Vector2(0f, 500f), 0f);
        screen.Render(new RecordingRenderer());
        ship.Pose = new ShipPose(new Vector2(0f, 1000f), 0f);
        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2(0f, 1000f), camera.Target);
    }

    [Fact]
    public void Render_LeavesTheShipInTheMiddleOfTheViewport()
    {
        // the whole point of the camera following: wherever the ship has flown to, it is on
        // screen, and it is in the middle
        Camera2D camera = new();
        ShipView ship = new(camera) { Pose = new ShipPose(new Vector2(-8000f, 250_000f), 1.2f) };
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();

        screen.Render(renderer);

        // Second from last: the ship is drawn over the stars and the scenery, and the frame is
        // drawn over the ship.
        Assert.Equal(renderer.ViewportCentre, renderer.Drawn[^2].Position);
    }

    [Fact]
    public void Render_FramesThePlayArea_OverEverythingElse()
    {
        // The frame is the last thing drawn, so the game passes beneath its dark corners rather
        // than behind them. Drawn earlier it would be scenery, and the ship would fly over it.
        GameScreen screen = ScreenFor(new Camera2D(), new StubShipView());
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(Vignette.AssetKey, 256, 256);

        screen.Render(renderer);

        Sprite frame = renderer.Drawn[^1];

        Assert.Equal(renderer.Textures.Load(Vignette.AssetKey), frame.Texture);
        Assert.Equal(renderer.ViewportCentre, frame.Position);
        Assert.Equal(renderer.ViewportSize.X, frame.Scale * frame.Stretch.X * 256f, precision: 3);
        Assert.Equal(renderer.ViewportSize.Y, frame.Scale * frame.Stretch.Y * 256f, precision: 3);
    }

    [Fact]
    public void Entering_TakesTheSkyDown_WithoutDimmingWhatIsFlownThrough()
    {
        // The backdrop read as a picture the game was happening in front of. What stands in the
        // world keeps its authored brightness, because the player has to see it in time.
        RegionView region = new(new Camera2D());
        GameScreen screen = ScreenFor(new Camera2D(), new StubShipView(), region: region);

        screen.Enter();

        Assert.Equal(
            new Colour(
                BattleForce2249.GameScreen.BackdropBrightness,
                BattleForce2249.GameScreen.BackdropBrightness,
                BattleForce2249.GameScreen.BackdropBrightness,
                1f),
            region.BackdropTint);
    }

    [Fact]
    public void TheRegionIsHandedTheClockItsLightsFlickerFrom_AndItStartsAtTheRegion()
    {
        // The view keeps no clock of its own, so if this stops reaching it the lights stand still
        // and nothing else changes — a failure with no symptom other than the one #188 reported.
        RegionView region = new(new Camera2D());
        GameScreen screen = ScreenFor(new Camera2D(), new StubShipView(), region: region);

        screen.Enter();
        screen.Update(TimeSpan.FromSeconds(0.25));
        screen.Update(TimeSpan.FromSeconds(0.5));
        screen.Render(new RecordingRenderer());

        Assert.Equal(0.75, region.SecondsElapsed, precision: 6);

        // Coming back to the game screen is a fresh look at the place: a light is not progress.
        screen.Enter();
        screen.Render(new RecordingRenderer());

        Assert.Equal(0, region.SecondsElapsed);
    }

    [Fact]
    public void EnteringTheScreen_BringsBackEveryHelpArrowTheLastFlightReached()
    {
        // A resumed or restarted game is a new approach to the field, not a continuation of the
        // one that last reached these arrows — the same reasoning the light's clock resets by.
        // Exercised directly against the shared HelpArrowView instance, rather than through
        // GameScreen.Render, because that always re-reads Bodies from the real loaded region and
        // there is no seam here to feed it a body of this test's own choosing.
        Camera2D camera = new();
        HelpArrowView helpArrows = new(camera);
        SceneBody arrow = new("Arrow", "Icon_Example02", 0, 0, 0, 1, 1, HelpArrowView.Layer, 0);
        GameScreen screen = ScreenFor(camera, new StubShipView(), helpArrows: helpArrows);

        helpArrows.Bodies = [arrow];
        helpArrows.ShipPosition = Vector2.Zero;
        helpArrows.Render(new RecordingRenderer());

        RecordingRenderer stillReached = new();
        helpArrows.ShipPosition = new Vector2(0, (float)HelpArrowView.FullyVisibleDistance);
        helpArrows.Render(stillReached);
        Assert.Empty(stillReached.Drawn);

        screen.Enter();

        RecordingRenderer afterReentering = new();
        helpArrows.Render(afterReentering);
        Assert.Single(afterReentering.Drawn);
    }

    [Fact]
    public void TheFrame_TakesNothingFromWhatIsUnderIt()
    {
        // The overlay is non-interactive: a frame with it drawn flies exactly the ship a frame
        // without it flies. It cannot be otherwise — a Vignette answers only Render — but the
        // requirement is the player's, so it is asserted from the player's end rather than from
        // the type's.
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 0) };

        GameScreen framed = ScreenFor(new Camera2D(), new StubShipView(), pilot: pilot);
        GameScreen bare = ScreenFor(new Camera2D(), new StubShipView(), pilot: pilot);

        framed.Render(new RecordingRenderer());
        framed.Update(TimeSpan.FromSeconds(1));
        bare.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(bare.Session.Player.Position.Y, framed.Session.Player.Position.Y, precision: 6);
        Assert.NotEqual(0d, framed.Session.Player.Position.Y);
    }

    [Fact]
    public void TheFirstFrame_PutsTheCameraStraightOntoTheShipsHeading()
    {
        // Snapped rather than eased, because there is nothing to ease from — a camera lagging on
        // its very first frame would swing the world round from due north to wherever the ship is
        // actually facing, as the player's opening move.
        Camera2D camera = new();
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 0, turn: 1) };
        GameScreen screen = ScreenFor(camera, new StubShipView(), pilot: pilot);

        screen.Update(TimeSpan.FromSeconds(0.2));

        screen.Render(new RecordingRenderer());

        Assert.Equal(camera.Orientation, screen.CameraHeading);
        Assert.NotEqual(0f, camera.Orientation);
    }

    [Fact]
    public void AsTheShipTurns_TheCameraLagsBehindIt_AndThenCatchesUp()
    {
        // The lag is the feature. Handed the heading outright the world snaps to every flick of
        // the helm, and the ship — drawn at its heading minus a camera orientation that is always
        // exactly its heading — never appears to turn at all. What is left behind by the camera
        // shows up as the ship leaning into its own turn.
        Camera2D camera = new();
        FixedShipInput pilot = new();
        GameScreen screen = ScreenFor(camera, new StubShipView(), pilot: pilot);

        screen.Update(TimeSpan.FromSeconds(0.1));   // settles the camera on the ship

        pilot.Controls = new ShipControls(thrust: 0, turn: 1);
        screen.Update(TimeSpan.FromSeconds(0.1));

        float shipHeading = (float)((StubGameSession)screen.Session).Ship.Movement.Heading;
        Assert.NotEqual(shipHeading, screen.CameraHeading);

        // and given long enough at that heading, it arrives.
        pilot.Controls = ShipControls.Neutral;
        for (int frame = 0; frame < 200; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60));
        }

        Assert.Equal(shipHeading, screen.CameraHeading, precision: 3);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void OnceTheCameraHasCaughtUp_TheShipPointsUpTheScreen(double turn)
    {
        // The two halves of the feature meeting: the screen turns the camera, the view takes the
        // camera's turn off the ship's own heading, and once the camera has caught up the player
        // sees a ship pointing up a world that has rotated around it. Asserted through the real
        // view rather than the stub, because it is the pair that has to agree.
        //
        // "Once it has caught up" rather than "always": the ship leans while the camera is still
        // coming round, which is deliberate and is what stops a turn feeling robotic.
        Camera2D camera = new();
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 0, turn: turn) };
        ShipView ship = new(camera);
        GameScreen screen = ScreenFor(camera, ship, pilot: pilot);

        // fly the turn, then hold the heading long enough for the camera to arrive
        for (int frame = 0; frame < 30; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60));
        }

        pilot.Controls = ShipControls.Neutral;
        for (int frame = 0; frame < 300; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60));
        }

        RecordingRenderer renderer = new();
        screen.Render(renderer);

        // last, because the ship is drawn over the stars and the scenery
        Assert.Equal(0f, renderer.Drawn[^1].Rotation, precision: 3);
        Assert.Equal(renderer.ViewportCentre, renderer.Drawn[^1].Position);
    }

    [Fact]
    public void MidTurn_TheShipLeansIntoIt()
    {
        // What the lag buys. The ship is the one thing on screen that never moved before this;
        // now it tips into a turn and settles upright when the turn is done.
        Camera2D camera = new();
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 0, turn: 1) };
        ShipView ship = new(camera);
        GameScreen screen = ScreenFor(camera, ship, pilot: pilot);

        screen.Update(TimeSpan.FromSeconds(1d / 60));   // settles the camera
        screen.Update(TimeSpan.FromSeconds(1d / 60));   // and now it is behind

        RecordingRenderer renderer = new();
        screen.Render(renderer);

        // Second from last: the frame is drawn over the ship, and a frame has no lean to read.
        Assert.NotEqual(0f, renderer.Drawn[^2].Rotation);
    }

    [Fact]
    public void Render_PutsTheStarsBehindTheShip()
    {
        // sprites stack in the order they are drawn, so the ship goes over the stars. The other way
        // round and the player flies behind their own background. The frame is drawn over both,
        // which is why the ship is second from last rather than last.
        Camera2D camera = new();
        ShipView ship = new(camera);
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.DefaultAssetKey, 512, 512);
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        screen.Render(renderer);

        Assert.True(
            renderer.Drawn.Count > 1,
            "The ship was the only thing drawn — the stars are missing.");
        Assert.Equal(512, renderer.Drawn[^2].Texture.Width);

        // The ship draws its engine glow beneath its hull, so what has to be stars is everything
        // before those: the hull, the glows and the frame over them all are the tail of the frame.
        Assert.All(
            renderer.Drawn.SkipLast(2 + ShipView.EngineGlows.Count),
            sprite => Assert.Equal(16, sprite.Texture.Width));
    }

    [Fact]
    public void Render_MovesTheStarsWhenTheShipFlies()
    {
        // the ship holds the middle of the viewport, so the stars are the only thing that can
        // show the player they are moving at all
        Camera2D camera = new();
        StubShipView ship = new();
        GameScreen screen = ScreenFor(camera, ship);
        RecordingRenderer renderer = new();

        screen.Render(renderer);
        Vector2[] standingStill = [.. renderer.Drawn.Select(sprite => sprite.Position)];

        renderer.Clear();
        ship.Pose = new ShipPose(new Vector2(0f, 200f), 0f);
        screen.Render(renderer);
        Vector2[] underWay = [.. renderer.Drawn.Select(sprite => sprite.Position)];

        Assert.NotEmpty(standingStill);
        Assert.NotEqual(standingStill, underWay);
    }

    [Fact]
    public void Update_DrawsNothing()
    {
        // update and draw are separate for a reason: a frame the platform does not draw still ticks
        StubShipView ship = new();
        GameScreen screen = ScreenFor(new Camera2D(), ship);

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(0, ship.Rendered);
    }

    // ---- handing the flight over to the drawing side ----

    [Fact]
    public void Update_PutsThePlayersPositionOnTheView()
    {
        StubShipView view = new();
        StubGameSession session = new();
        GameScreen screen = ScreenFor(new Camera2D(), view, session);
        session.Player.MoveTo(new Position(120, -45));

        screen.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Equal(new Vector2(120f, -45f), view.Pose.Position);
    }

    [Fact]
    public void Update_LeavesTheViewAlone_BeforeTheGameHasStarted()
    {
        // frames arrive while the save is still being read; there is no position to draw yet, and
        // drawing the ship at the origin would put it somewhere the player has never been
        StubShipView view = new() { Pose = new ShipPose(new Vector2(7f, 8f), 0.5f), Thrust = 0.75f };
        GameScreen screen = ScreenFor(new Camera2D(), view, new StubGameSession { IsReady = false });

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(new ShipPose(new Vector2(7f, 8f), 0.5f), view.Pose);
        Assert.Equal(0.75f, view.Thrust);
    }

    [Fact]
    public void Update_PutsThePilotsThrustOnTheView()
    {
        // the engine glow reads this, and it has to be the burn that actually flew the ship this
        // frame — not a second, later read of a pilot whose controls could have moved on already
        StubShipView view = new();
        GameScreen screen = ScreenFor(
            new Camera2D(),
            view,
            pilot: new FixedShipInput { Controls = new ShipControls(thrust: -1, turn: 0) });

        screen.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Equal(-1f, view.Thrust);
    }

    [Fact]
    public void Update_PutsTheHeadingOnTheView_TheSameWayRoundTheShipMeasuresIt()
    {
        // the one thing neither type can enforce: both sides call zero straight forward and count
        // the angle to starboard, so the heading passes through untouched. Negate it — or measure
        // it the other way round on either side — and the sprite turns against the flight.
        StubShipView view = new();
        StubGameSession session = new();
        GameScreen screen = ScreenFor(
            new Camera2D(),
            view,
            session,
            pilot: new FixedShipInput { Controls = new ShipControls(thrust: 1, turn: 1) });

        screen.Update(TimeSpan.FromSeconds(0.2));

        ShipMovement ship = session.Ship.Movement;
        Assert.Equal((float)ship.Heading, view.Pose.Heading);
        Assert.True(ship.Heading > 0, "a turn to starboard should raise the heading");
        Assert.True(view.Pose.Position.X > 0, "a turn to starboard should carry the ship towards +X");
    }

    [Fact]
    public void Render_FollowsTheShipThatWasJustFlown()
    {
        // update to draw, end to end: what the physics produced this frame is where the camera
        // looks, without a test putting a pose there by hand
        StubShipView view = new();
        StubGameSession session = new();
        Camera2D camera = new();
        GameScreen screen = ScreenFor(
            camera,
            view,
            session,
            pilot: new FixedShipInput { Controls = FullAhead });

        screen.Update(TimeSpan.FromSeconds(1));
        screen.Render(new RecordingRenderer());

        Assert.Equal(new Vector2((float)session.Player.Position.X, (float)session.Player.Position.Y), camera.Target);
        Assert.True(camera.Target.Y > 1, "the ship went nowhere, so this proves nothing");
    }

    IGameSession Session => Resolve<IGameSession>();

    IGameScreen GameScreen => Resolve<IGameScreen>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    /// <param name="configure">Anything else the test needs registered, such as a pilot.</param>
    IHost StartTheGame(Action<IServiceCollection>? configure = null)
    {
        Configure(services: services =>
        {
            services
                // the real UI controller, so pressing the start button raises its action
                .AddSingleton<IUIController, UIController>()
                // the shipping director, so navigating a screen actually enters it
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>();

            configure?.Invoke(services);
        });

        IHost host = CreateHost();
        host.Start();
        host.Update(TimeSpan.FromDays(1));                      // company screen elapses

        MenuScreen menu = (MenuScreen)Resolve<IMenuScreen>();
        for (int frame = 0; !menu.IsReadyForInput; frame++)
        {
            Assert.True(frame < 1000, "the menu never became ready for input");
            host.Update(TimeSpan.Zero);
        }

        menu.Press();
        menu.Release();

        return host;
    }

    [Fact]
    public void EnteringTheScreen_StartsTheGame()
    {
        ((IActivatable)GameScreen).Enter();

        Assert.True(Session.IsReady);
    }

    [Fact]
    public void TheGameScreenIsActive_OnceTheMenuRequestsAStart()
    {
        StartTheGame();

        Assert.Same(GameScreen, ScreenDirector.Current);
    }

    [Fact]
    public void Quest1_IsActive_AfterTheFirstFrameOfANewGame()
    {
        // the screen, not the quest model, notices the player is on the start marker
        IHost host = StartTheGame();

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Quest quest = Assert.Single(Session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Fact]
    public void Quest1_StaysInProgress_UntilTheEndMarkerIsReached()
    {
        IHost host = StartTheGame();

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void MovingForwardAcrossFrames_CompletesQuest1()
    {
        IHost host = StartTheGame();
        QuestMarkers markers = Resolve<IWorld>().QuestMarkers
            .Single(marker => marker.QuestId == BattleForceCampaign.Quest1Id);

        // the quest path on its own, with the player moved by hand rather than flown: the ship is
        // covered by the tests below, and this one is about the markers
        for (int frame = 0; !Session.Quests.Completed.Any(); frame++)
        {
            Assert.True(frame < 10_000, "the player never completed the quest");
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
            Session.Player.MoveBy(0, 10);
        }

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Empty(Session.Quests.Active);
        Assert.True(Session.Player.Position.DistanceTo(markers.End) <= 50 + 10);
    }

    [Fact]
    public void DoesNotDriveQuests_BeforeTheGameHasStarted()
    {
        // frames can arrive while the save is still loading
        GameScreen screen = (GameScreen)GameScreen;

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Empty(Session.Quests.Quests);
    }

    // ---- flying the ship ----

    static readonly ShipControls FullAhead = new(thrust: 1, turn: 0);

    /// <summary>
    /// Runs the game for a stretch of 60Hz frames, stopping early once <paramref name="until"/> is
    /// satisfied so a test that is waiting for something does not have to guess how long it takes.
    /// </summary>
    static void Play(IHost host, int frames, Func<bool>? until = null)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            if (until?.Invoke() is true)
            {
                return;
            }

            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }
    }

    [Fact]
    public void FlyingDeadStraightFromANewGame_IsBlockedByTheDebrisField_AndQuest1StaysInProgress()
    {
        // Flying straight through used to reach the exit marker unimpeded, because the debris
        // field was scenery and nothing else. It is not any more: the field is dense enough that
        // holding one heading runs the ship into a rock long before 1000 units, and that is
        // correct — a "collapsing debris field" a ship can fly straight through regardless is not
        // one, and the shipped field has no route that wide. Getting clear now takes piloting,
        // which is a content question for a human at a screen to answer with the field's own
        // layout, not something this test should fake its way past with a smarter autopilot.
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60 * 10);

        Assert.True(Session.Player.Position.Y > 50, "the ship never got into the field at all");
        Assert.True(Session.Player.Position.Y < 900, "the ship reached the exit marker unimpeded");
        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void HandsOff_TheShipStaysPut_AndQuest1StaysInProgress()
    {
        // no pilot registered, so the engine's neutral input is what the ship is flown on
        IHost host = StartTheGame();

        Play(host, frames: 600);

        Assert.Equal(new Position(0, 0), Session.Player.Position);
        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }

    [Fact]
    public void TurningTakesTheShipOffTheForwardAxis()
    {
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 1) };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60);

        Assert.True(Math.Abs(Session.Player.Position.X) > 1, "the helm did not steer the ship");
    }

    [Fact]
    public void TheShipFliesBeforeTheQuestsAreMeasured()
    {
        // ordering, and it is load bearing: a quest measured against last frame's position fires a
        // frame late, which at speed is a marker the player has already gone past
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);                                  // the start marker begins quest 1
        Assert.Single(Session.Quests.Active);

        // 60 units short of the exit marker, which fires within 50; one second of thrust from rest
        // covers about 68, so this frame is the frame that arrives
        Session.Player.MoveTo(new Position(0, 940));
        pilot.Controls = FullAhead;

        host.Update(TimeSpan.FromSeconds(1));

        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void ALongFrameThatFliesStraightPastTheExitMarker_StillCompletesQuest1()
    {
        // the trap pillar 1 names, through the real composition. Quest 1's exit marker is 1000
        // units forward with a 50 unit trigger; a single frame long enough to carry the ship from
        // well short of it to well past it lands on neither side of that trigger, and point
        // sampling would have flown the player straight through the objective.
        FixedShipInput pilot = new();
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 1);                                  // the start marker begins quest 1
        Assert.Single(Session.Quests.Active);

        Session.Player.MoveTo(new Position(0, 900));             // outside the 50 unit trigger
        pilot.Controls = FullAhead;

        host.Update(TimeSpan.FromSeconds(5));                    // one frame, straight past the exit

        Assert.True(Session.Player.Position.Y > 1050, "the frame did not carry the ship past the marker");
        Assert.Single(Session.Quests.Completed);
    }

    [Fact]
    public void FlyingTheShip_MovesTheOneOnScreen()
    {
        // through the real composition, because the seam is only worth anything if the ship the
        // logic side flies is the ship the drawing side draws
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60);

        ShipPose pose = Resolve<IShipView>().Pose;
        Assert.Equal((float)Session.Player.Position.X, pose.Position.X);
        Assert.Equal((float)Session.Player.Position.Y, pose.Position.Y);
        Assert.True(pose.Position.Y > 1, "the ship never left the start marker");
    }

    [Fact]
    public async Task Render_SurvivesASaveWhosePositionIsBeyondWhatTheCameraCanBeAimedAt()
    {
        // The route that makes refusing a non-finite camera target safe to land. A coordinate of
        // 1e300 is a finite double and valid JSON, so nothing on the way in used to stop it — but
        // GameScreen.PoseOf narrows it to a float on the way to the camera, where it becomes an
        // infinity. Before the camera guard that was a blank screen; with the guard and nothing
        // else it would be an exception once per frame, out of the frame loop, which is a worse
        // failure than the one this issue set out to fix. The save is refused instead, exactly as
        // a save that will not parse already is.
        //
        // This is not a ship that flew too far — it cannot, in any amount of time. The number
        // arrives fully formed from a file.
        InMemorySaveProgressService saves = new()
        {
            Content = $$"""
            {
              "PlayerX": 1e300,
              "PlayerY": 1e300,
              "Quests": [ { "QuestId": "{{BattleForceCampaign.Quest1Id}}", "State": "Active" } ]
            }
            """,
        };

        IHost host = StartTheGame(services => services.AddSingleton<ISaveProgressService>(saves));
        await ((GameScreen)GameScreen).Started;

        Play(host, frames: 1);
        ((IRenderable)GameScreen).Render(new RecordingRenderer());

        // a new game at the world start, which is what the game already does with a file it
        // cannot read — not a crash, and not a blank window either
        Assert.Equal(new BattleForceWorld().PlayerStart, Session.Player.Position);
        Assert.Equal(Vector2.Zero, Resolve<ICamera>().Target);
    }

    [Fact]
    public async Task EnteringTheScreen_BringsTheShipToRest()
    {
        // The save carries where the player is, never how fast they were going. Nothing resets the
        // ship to get this any more: entering the screen resumes the game, resuming builds a new
        // ship from the character's hull, and a new ship has never been anywhere.
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 1) };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));
        Play(host, frames: 120);

        Assert.NotEqual(Velocity.Stationary, Session.Ship.Movement.Velocity);

        ((IActivatable)GameScreen).Enter();
        await ((GameScreen)GameScreen).Started;

        ShipMovement ship = Session.Ship.Movement;
        Assert.Equal(Velocity.Stationary, ship.Velocity);
        Assert.Equal(0, ship.Heading);
    }
}
