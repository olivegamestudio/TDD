using System.Numerics;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// QA's adversarial cover for #16 — the ship on screen. The tests either side of this one assert
/// that the drawing path does what it was asked to; these try to make it stop, by flying to the
/// edges of the numbers rather than around the origin.
/// </summary>
/// <remarks>
/// The invariant every one of these is aimed at is the one the issue was raised about: a ship that
/// is not on screen is indistinguishable from a ship that was never drawn. The camera follows, so
/// the ship holds the middle of the viewport — and it has to hold it after an hour of flying, after
/// a turn that has been round more than once, and before the save has even been read.
///
/// Guarding a non-finite <see cref="ICamera.Target"/> or <see cref="ICamera.PixelsPerUnit"/> is
/// deliberately not tried here: it is #57, raised against the camera rather than against anything
/// on this branch.
/// </remarks>
public sealed class ShipOnScreenAdversarialTests
{
    /// <summary>
    /// A session that can be held un-ready, so a frame arriving before the save has been read can
    /// be driven the way the platform host would drive it.
    /// </summary>
    sealed class LoadingGameSession : IGameSession
    {
        public Player Player { get; } = new();

        public QuestLog Quests { get; } = new();

        public bool IsReady { get; set; }

        public Task PendingSave => Task.CompletedTask;

        public Task StartNewGame() => Task.CompletedTask;

        public Task Continue() => Task.CompletedTask;

        public Task Save() => Task.CompletedTask;
    }

    static GameScreen ScreenFor(
        ICamera camera,
        IShipView view,
        IGameSession session,
        IShipInput? pilot = null,
        ShipMovement? ship = null) =>
        new(session,
            ship ?? new ShipMovement(DisgracedShip.Handling),
            pilot ?? new NeutralShipInput(),
            new QuestProximityWatcher(new BattleForceWorld()),
            camera,
            view);

    // ---- before there is a game to draw ----

    [Fact]
    public void Render_BeforeTheSaveHasBeenRead_DrawsTheShipAtTheWorldOrigin()
    {
        // frames arrive while the save is still loading and the platform host draws every one of
        // them. Nothing sets a pose yet, so the ship is drawn where a new game begins rather than
        // at some position left over in memory — and the camera is on it, so it is still on screen
        Camera2D camera = new();
        ShipView view = new(camera);
        LoadingGameSession session = new() { IsReady = false };
        GameScreen screen = ScreenFor(camera, view, session);
        RecordingRenderer renderer = new();

        screen.Update(TimeSpan.FromSeconds(1d / 60d));
        screen.Render(renderer);

        Assert.Equal(Vector2.Zero, camera.Target);
        Assert.Equal(renderer.ViewportCentre, renderer.Single().Position);
    }

    [Fact]
    public void Update_BeforeTheSaveHasBeenRead_DoesNotFlyTheShipOnScreen()
    {
        // full thrust with nobody home: the frame must not move the player, or a save read a
        // moment later would be resumed at a position the player never flew to
        Camera2D camera = new();
        ShipView view = new(camera);
        LoadingGameSession session = new() { IsReady = false };
        GameScreen screen = ScreenFor(
            camera,
            view,
            session,
            new FixedShipInput { Controls = new ShipControls(1, 0) });

        for (int frame = 0; frame < 60; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60d));
        }

        Assert.Equal(Position.Origin, session.Player.Position);
        Assert.Equal(default, view.Pose);
    }

    // ---- a long way from the origin ----

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-8_000f, 250_000f)]
    [InlineData(1_000_000f, -1_000_000f)]
    [InlineData(1e12f, 1e12f)]
    public void Render_LeavesTheShipExactlyInTheMiddle_HoweverFarItHasFlown(float x, float y)
    {
        // the camera's target and the ship's position are the same numbers, so the subtraction in
        // WorldToScreen cancels exactly however large they get. If that ever stops being true the
        // ship drifts off centre far from the origin, which is a bug nobody would find near it
        Camera2D camera = new();
        ShipView view = new(camera) { Pose = new ShipPose(new Vector2(x, y), 1.2f) };
        GameScreen screen = ScreenFor(camera, view, new LoadingGameSession { IsReady = true });
        RecordingRenderer renderer = new();

        screen.Render(renderer);

        Assert.Equal(renderer.ViewportCentre, renderer.Single().Position);
    }

    [Fact]
    public void Render_LeavesTheShipInTheMiddle_EveryFrameOfATurningFlight()
    {
        // ten seconds of full burn with the helm hard over, drawn every frame: the ship holds the
        // middle throughout, so nothing about flying or turning can slide it off screen
        Camera2D camera = new() { PixelsPerUnit = 2.5f };
        ShipView view = new(camera);
        LoadingGameSession session = new() { IsReady = true };
        GameScreen screen = ScreenFor(
            camera,
            view,
            session,
            new FixedShipInput { Controls = new ShipControls(1, 1) });
        RecordingRenderer renderer = new();

        for (int frame = 0; frame < 600; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60d));
            renderer.Clear();
            screen.Render(renderer);

            Assert.Equal(renderer.ViewportCentre, renderer.Single().Position);
        }

        // and it really did fly somewhere, so the assertion above was not about a stationary ship
        Assert.NotEqual(Position.Origin, session.Player.Position);
    }

    // ---- the heading handed over ----

    [Fact]
    public void Update_HandsOverAHeadingInsideOneTurn_HoweverLongTheShipKeepsTurning()
    {
        // the helm held over for a minute is several full circles; the pose must carry an angle
        // inside [0, 2π) rather than an ever growing one, because a rotation that grows without
        // bound loses precision as a float long before it stops being drawable
        Camera2D camera = new();
        ShipView view = new(camera);
        GameScreen screen = ScreenFor(
            camera,
            view,
            new LoadingGameSession { IsReady = true },
            new FixedShipInput { Controls = new ShipControls(0, 1) });

        for (int frame = 0; frame < 3_600; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60d));

            Assert.InRange(view.Pose.Heading, 0f, (float)Math.Tau);
        }
    }

    [Fact]
    public void Render_PointsTheShipWhereItIsFacing_NotWhereItIsTravelling()
    {
        // momentum is most of what separates flying from being dragged around by the controls, so
        // a ship that burns forward and then turns is still travelling forward while facing across
        // it. The sprite follows the nose; a sprite that followed the velocity would hide the drift
        Camera2D camera = new();
        ShipView view = new(camera);
        FixedShipInput pilot = new() { Controls = new ShipControls(1, 0) };
        ShipMovement ship = new(DisgracedShip.Handling);
        GameScreen screen = ScreenFor(
            camera,
            view,
            new LoadingGameSession { IsReady = true },
            pilot,
            ship);
        RecordingRenderer renderer = new();

        for (int frame = 0; frame < 120; frame++)
        {
            screen.Update(TimeSpan.FromSeconds(1d / 60d));
        }

        pilot.Controls = new ShipControls(0, 1);
        screen.Update(TimeSpan.FromSeconds(0.25));
        screen.Render(renderer);

        // still carrying most of its way forward...
        Assert.True(ship.Velocity.Y > 0, "the ship should have kept the momentum it built up");

        // ...while the sprite has turned with the nose
        Assert.Equal((float)ship.Heading, renderer.Single().Rotation);
        Assert.NotEqual(0f, renderer.Single().Rotation);
    }

    // ---- frames that cover no time ----

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-10_000_000L)]
    public void Update_WithAFrameThatCoversNoTime_LeavesTheShipWhereItWas(long ticks)
    {
        // a paused frame, or a clock that has gone backwards. Either way the ship holds still and
        // the pose it hands over is the one it already had, rather than a NaN nobody can draw
        Camera2D camera = new();
        ShipView view = new(camera);
        LoadingGameSession session = new() { IsReady = true };
        GameScreen screen = ScreenFor(
            camera,
            view,
            session,
            new FixedShipInput { Controls = new ShipControls(1, 1) });

        screen.Update(TimeSpan.FromSeconds(1));
        ShipPose flown = view.Pose;

        screen.Update(TimeSpan.FromTicks(ticks));

        Assert.Equal(flown, view.Pose);
    }

    // ---- the asset key is an identifier ----

    [Fact]
    public void AssetKey_TellsTwoKeysApartByCase_BecauseItNamesAFileAndIsNeverTranslated()
    {
        // "Ship1" is not "ship1" — an asset key is an identifier, and a case-insensitive match
        // would quietly keep drawing the old sprite on a platform whose content is case sensitive
        Camera2D camera = new();
        ShipView view = new(camera);
        RecordingRenderer renderer = new();

        view.Render(renderer);
        view.AssetKey = "Ship1";
        view.Render(renderer);

        Assert.Equal(["ship1", "Ship1"], renderer.Textures.Requested);
    }

    // ---- the sprite is drawable whatever the artwork is ----

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 7)]
    [InlineData(4096, 4096)]
    public void Render_DrawsTheShipAtItsLengthInWorldUnits_WhateverSizeTheArtworkIs(int width, int height)
    {
        // the scale is derived from the texture, so replacing the sprite changes nothing about how
        // big the ship is in the world. A scale that came out zero, negative or non-finite would be
        // a ship nobody can see, which is this issue's report exactly
        Camera2D camera = new();
        ShipView view = new(camera);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(ShipView.DefaultAssetKey, width, height);

        view.Render(renderer);

        Sprite drawn = renderer.Single();
        Assert.Equal(ShipView.LengthInWorldUnits / height, drawn.Scale);
        Assert.True(float.IsFinite(drawn.Scale) && drawn.Scale > 0f, "the ship must be drawable");
        Assert.Equal(new Vector2(width / 2f, height / 2f), drawn.Origin);
    }
}
