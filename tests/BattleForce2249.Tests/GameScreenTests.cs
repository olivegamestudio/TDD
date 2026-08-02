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

        public void Render(IRenderer renderer) => Rendered++;
    }

    /// <summary>
    /// A session that is ready the moment it is built, so a screen assembled by hand can be
    /// driven a frame at a time without a save being read first. The drawing tests care about
    /// what reaches the view, not about where the game came from.
    /// </summary>
    sealed class StubGameSession : IGameSession
    {
        public Player Player { get; } = new();

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
        ShipMovement? ship = null) =>
        new(session ?? new StubGameSession(),
            ship ?? new ShipMovement(DisgracedShip.Handling),
            pilot ?? new NeutralShipInput(),
            new QuestProximityWatcher(new BattleForceWorld()),
            camera,
            view,
            new StarField(camera));

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

        // last, because the ship is drawn over the stars
        Assert.Equal(renderer.ViewportCentre, renderer.Drawn[^1].Position);
    }

    [Fact]
    public void Render_PutsTheStarsBehindTheShip()
    {
        // sprites stack in the order they are drawn, so the ship goes last. The other way round
        // and the player flies behind their own background.
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
        Assert.Equal(512, renderer.Drawn[^1].Texture.Width);
        Assert.All(renderer.Drawn.SkipLast(1), sprite => Assert.Equal(16, sprite.Texture.Width));
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
        StubShipView view = new() { Pose = new ShipPose(new Vector2(7f, 8f), 0.5f) };
        GameScreen screen = ScreenFor(new Camera2D(), view, new StubGameSession { IsReady = false });

        screen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(new ShipPose(new Vector2(7f, 8f), 0.5f), view.Pose);
    }

    [Fact]
    public void Update_PutsTheHeadingOnTheView_TheSameWayRoundTheShipMeasuresIt()
    {
        // the one thing neither type can enforce: both sides call zero straight forward and count
        // the angle to starboard, so the heading passes through untouched. Negate it — or measure
        // it the other way round on either side — and the sprite turns against the flight.
        StubShipView view = new();
        ShipMovement ship = new(DisgracedShip.Handling);
        GameScreen screen = ScreenFor(
            new Camera2D(),
            view,
            pilot: new FixedShipInput { Controls = new ShipControls(thrust: 1, turn: 1) },
            ship: ship);

        screen.Update(TimeSpan.FromSeconds(0.2));

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
    public void FlyingForwardFromANewGame_GetsClearOfTheDebrisField_AndCompletesQuest1()
    {
        // the whole point of the ticket: quest 1 completed by playing rather than by a test
        // reaching in and moving the player
        FixedShipInput pilot = new() { Controls = FullAhead };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));

        Play(host, frames: 60 * 30, until: () => Session.Quests.Completed.Any());

        Quest quest = Assert.Single(Session.Quests.Completed);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Empty(Session.Quests.Active);
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
    public void EnteringTheScreen_BringsTheShipToRest()
    {
        // the save carries where the player is, never how fast they were going
        FixedShipInput pilot = new() { Controls = new ShipControls(thrust: 1, turn: 1) };
        IHost host = StartTheGame(services => services.AddSingleton<IShipInput>(pilot));
        Play(host, frames: 120);

        ((IActivatable)GameScreen).Enter();

        ShipMovement ship = Resolve<ShipMovement>();
        Assert.Equal(Velocity.Stationary, ship.Velocity);
        Assert.Equal(0, ship.Heading);
    }
}
