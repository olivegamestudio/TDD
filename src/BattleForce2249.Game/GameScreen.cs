using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. Entering it begins or resumes the game session; every frame
/// it flies the ship, measures the player against the quest markers and drives the quest API from
/// what it finds — proximity is the presentation's job, not the quest model's — and then hands the
/// result to the drawing side as a pose. Drawing follows the ship rather than leading it.
/// </summary>
/// <param name="session">The game in progress, including the ship it is being flown in.</param>
/// <param name="pilot">Where the pilot's intent comes from this frame.</param>
/// <param name="questProximity">Applies the quests' proximity triggers against the world.</param>
/// <param name="camera">The camera the world is drawn through.</param>
/// <param name="view">The ship on screen, which draws whatever pose it was last given.</param>
/// <param name="stars">The stars the ship flies through.</param>
/// <param name="region">The scenery of the place being flown through.</param>
/// <param name="regions">Where an authored region is read from.</param>
/// <param name="frame">The overlay the play area is framed with.</param>
/// <param name="collisionDebug">The developer overlay that draws collision shapes over everything.</param>
public sealed class GameScreen(
    IGameSession session,
    IShipInput pilot,
    QuestProximityWatcher questProximity,
    ICamera camera,
    IShipView view,
    StarField stars,
    RegionView region,
    RegionLoader regions,
    Vignette frame,
    CollisionDebugView collisionDebug)
    : IGameScreen, IActivatable, IRenderable
{
    /// <summary>
    /// How close the camera sits to the world, in pixels per world unit.
    /// </summary>
    /// <remarks>
    /// Set here rather than left at the camera's default of one, which was chosen when there was
    /// nothing on screen but stars and a ship and so had nothing to be right or wrong about. With
    /// scenery to fly through, the zoom decides how much debris is in view at once — and therefore
    /// how much warning the player gets of what they are flying into, which is a gameplay number
    /// rather than a rendering one.
    /// </remarks>
    public const float Zoom = 2.5f;

    /// <summary>
    /// How quickly the camera comes round to the ship's heading, as a proportion of the angle
    /// still to cover each second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camera used to be handed the ship's heading outright, which made the world snap to
    /// every flick of the helm and pinned the ship rigidly upright — it never appeared to turn at
    /// all, because it was drawn at its heading minus a camera orientation that was always exactly
    /// its heading. That is what read as robotic: the ship is the one thing on screen that never
    /// moves.
    /// </para>
    /// <para>
    /// Lagging the camera fixes both ends of it. The world comes about smoothly instead of
    /// jumping, and the difference between where the ship is pointed and where the camera has got
    /// to shows up as the ship leaning into its own turn and settling once the turn is done.
    /// </para>
    /// </remarks>
    public const float CameraCatchUpPerSecond = 6f;

    /// <summary>
    /// The region the game opens in — the collapsing debris field the Disgraced escapes from.
    /// An identifier, never translated.
    /// </summary>
    public const string OpeningRegionId = "debris-field";

    /// <summary>
    /// How brightly the sky behind the game is drawn, against the 1 it was authored at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backdrop was authored as a picture in its own right and read as one: bright and flat
    /// enough that the ship and the debris in front of it had nothing to stand out against. Taking
    /// it down is what puts it behind the game rather than beside it.
    /// </para>
    /// <para>
    /// It reaches the backdrop only — see <see cref="RegionView.BackdropTint"/>. The stars are
    /// untouched, because points of light on a darker sky is the contrast this is buying, and so
    /// is the scenery in the world, which the player has to see in time to avoid.
    /// </para>
    /// <para>
    /// Untuned, and said plainly: this is a number chosen by reasoning about it rather than by
    /// looking at it, and it is a judgement only a human at a screen can settle.
    /// </para>
    /// </remarks>
    public const float BackdropBrightness = 0.5f;

    /// <summary>
    /// Gets the task that begins the game, so a caller can await the save being read. Loading
    /// happens off the frame loop; nothing drives the session until it has finished.
    /// </summary>
    public Task Started { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Begins the game: the saved game is resumed, or a new one is started when there is no save.
    /// </summary>
    /// <returns>Always <see cref="EnterResult.Stay"/>; this screen is where gameplay happens.</returns>
    public EnterResult Enter()
    {
        // A fresh flight every time the screen is entered: the save carries where the player is,
        // never how fast they were going, so nothing is inherited from a previous session. Nothing
        // is reset here to get that — starting or resuming builds a new ship, and a new ship is at
        // rest facing forward because that is what a new one is.
        // The one place there is, until the world is streamed by proximity: entering the game puts
        // the player in the debris field the opening is set in. Loaded here rather than in the
        // constructor because a region is content read from disk, and reading it is work that
        // belongs to entering a screen rather than to building the container.
        region.Scene = regions.Load(OpeningRegionId);

        // Beside the zoom rather than in the region file, because both are decisions about how
        // this screen looks rather than about what the place contains: a region is content, and
        // content that carried its own brightness would have to be re-authored to change its mood.
        region.BackdropTint = new Colour(BackdropBrightness, BackdropBrightness, BackdropBrightness, 1f);

        camera.PixelsPerUnit = Zoom;
        _cameraSettled = false;

        // The region's lights are drawn from a clock rather than from anything the player did, so
        // it starts where the region does. Coming back to the game screen is a fresh look at the
        // place, not a resumption of one — nothing about a light is progress.
        _secondsInRegion = 0;

        // A ship built for a game already entered has whatever obstacles that game's region put
        // in it; a ship this Enter is about to build has none, and needs the field it flies into
        // seeded again. Cleared here rather than inferred from the ship changing, because the
        // check that infers it runs every frame and only needs to be right, not cheap to read.
        _obstacleShip = null;

        Started = session.Continue();
        return EnterResult.Stay;
    }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        // Before the guard below, deliberately: the region is loaded and drawn from the moment the
        // screen is entered, so its lights are on screen while the save is still being read and
        // would otherwise stand frozen until it finished.
        _secondsInRegion += frameTime.TotalSeconds;

        if (!session.IsReady)
        {
            // frames arrive while the save is still being read; there is no position to draw yet,
            // so the view keeps the pose it has rather than being shown a half loaded game
            return;
        }

        // where the frame began, taken before the ship flies. The quests are measured against the
        // journey between this and where the ship ends up, not against the end of it: at 200 units
        // a second a long frame steps clean over a 50 unit trigger, and a marker that fires only
        // when a frame happens to land inside it is the trap pillar 1 names.
        Position began = session.Player.Position;

        // read per frame rather than held, because starting or resuming the game replaces the ship;
        // a screen holding the one it was built with would go on flying a ship nobody is in
        Ship shipEntity = session.Ship;
        ShipMovement ship = shipEntity.Movement;

        // The region is loaded synchronously in Enter, but the ship is not ready until session
        // .Continue finishes — off the frame loop, on its own time — so seeding waits for the
        // first frame both exist together rather than trying to do it in Enter itself. Compared
        // by reference against the ship rather than a bool: starting a new game after resuming
        // one, or the other way round, builds a second ship the first one's obstacles say nothing
        // about.
        if (!ReferenceEquals(_obstacleShip, shipEntity))
        {
            RegionObstacles.Seed(region.Scene, ship);
            _obstacleShip = shipEntity;
        }

        // the ship flies first, so the quests are measured against where the player got to this
        // frame rather than where they were at the end of the last one
        ship.Update(session.Player, pilot.Read(), frameTime);

        questProximity.Update(session.Quests, began, session.Player.Position);

        // and last, what the frame produced is handed to the drawing side. The pose is the whole
        // of what the two stages agree about: the physics has no idea a screen exists, and the
        // view has no idea physics does.
        view.Pose = PoseOf(session.Player.Position, ship.Heading);

        // The first frame of a game snaps rather than eases. There is nothing to ease from — the
        // camera has never been anywhere — so lagging it would swing the world round from due
        // north to wherever the ship is actually facing, as the player's opening move.
        _cameraHeading = _cameraSettled
            ? CatchUp(_cameraHeading, (float)ship.Heading, frameTime)
            : (float)ship.Heading;

        _cameraSettled = true;
    }

    /// <summary>
    /// Where the camera has got to in coming round to the ship's heading. Held here rather than on
    /// the camera because it is a decision about how this game feels to fly, not a property of a
    /// camera — another screen pointing the same camera at something else should not inherit it.
    /// </summary>
    float _cameraHeading;

    /// <summary>
    /// Gets where the camera has got to in coming round to the ship's heading, in radians.
    /// Exposed so the lag itself can be asserted on: it is the difference between this and the
    /// ship's heading that the player reads as the ship leaning into a turn.
    /// </summary>
    public float CameraHeading => _cameraHeading;

    /// <summary>
    /// Gets the game this screen is playing, so a test can see what the ship it is drawing is
    /// actually doing.
    /// </summary>
    public IGameSession Session => session;

    /// <summary>
    /// Whether the camera has a heading to catch up <em>from</em>. Cleared on entering, so
    /// starting a game or coming back to one begins pointed where the ship is pointed rather than
    /// swinging round to find it.
    /// </summary>
    bool _cameraSettled;

    /// <summary>
    /// How long the region has been on screen, in seconds. Held here rather than in the view for
    /// the reason the camera's heading is: the view is handed what to draw and does not keep a
    /// clock of its own, so a frame drawn twice draws the same picture both times.
    /// </summary>
    double _secondsInRegion;

    /// <summary>
    /// Which ship <see cref="RegionObstacles"/> has already been seeded into this game, so a ship
    /// already flying a field of obstacles is not handed a second copy of the same field every
    /// frame. <c>null</c> means the current ship, once there is one, has not been seeded yet.
    /// </summary>
    Ship? _obstacleShip;

    /// <summary>
    /// Eases one angle towards another, taking the short way round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The step is a proportion of what is left rather than a fixed number of radians, so a hard
    /// turn moves quickly and the last few degrees settle gently — a constant rate arrives at the
    /// heading and stops dead, which is the robotic feel this exists to avoid.
    /// </para>
    /// <para>
    /// Framed against elapsed time rather than per frame, so the camera behaves the same on a slow
    /// machine as a fast one. Wrapped to the shorter arc, or coming about past due north would
    /// send the camera the whole way round the compass to reach a heading a few degrees away.
    /// </para>
    /// </remarks>
    static float CatchUp(float from, float to, TimeSpan frameTime)
    {
        float difference = to - from;

        // Into the range −π..π, so the camera turns the short way.
        difference -= MathF.Tau * MathF.Round(difference / MathF.Tau);

        float caught = 1f - MathF.Exp(-CameraCatchUpPerSecond * (float)frameTime.TotalSeconds);

        return from + (difference * caught);
    }

    /// <summary>
    /// Leaves the game screen. Progress is saved as quests change, so there is nothing to flush here.
    /// </summary>
    public void Exit()
    {
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        // The camera follows the ship rather than standing still. At the speeds this ship flies,
        // a fixed viewport is left behind within seconds of the player touching the throttle,
        // and a ship that has flown off the edge of the screen is indistinguishable from one
        // that was never drawn. The world moves; the ship holds the middle.
        camera.Target = view.Pose.Position;

        // And turned to the ship's heading, which is what keeps the ship's nose at the top of the
        // window: the world rotates around the ship rather than the ship rotating within a world
        // that stays upright. Set here beside the target because the two are one decision — where
        // the camera is and which way it is facing — and a camera that followed the ship's
        // position but not its heading would leave the ship spinning on the spot again.
        camera.Orientation = _cameraHeading;

        // Before the ship, so the ship is over the stars rather than behind one. It is also what
        // makes the ship look like it is going anywhere: the camera holds the ship still in the
        // middle of the viewport, so the only thing that can move is what is behind it.
        stars.Render(renderer);

        // Between the stars and the ship: the scenery is in the world, so it passes in front of the
        // starfield and behind the thing flying through it.
        //
        // Handed the clock its lights flicker from, here beside the camera's own state, because
        // this is where the frame's results reach the drawing side.
        region.SecondsElapsed = _secondsInRegion;
        region.Render(renderer);

        view.Render(renderer);

        // So the frame is over the game rather than in it: the ship and the debris pass beneath
        // the dark corners instead of behind them. It draws and nothing else, so nothing under it
        // loses a press to it.
        frame.Render(renderer);

        // Last of all and over the frame too, deliberately: a collision worth checking near the
        // edge of the screen is not one that should be dimmed to find. See CollisionDebugView's
        // own remarks for why this exists and when it should stop defaulting to drawn.
        collisionDebug.ShipPosition = view.Pose.Position;
        collisionDebug.HullRadius = session.Ship.Profile.HullRadius;
        collisionDebug.Bodies = region.Scene.Bodies;
        collisionDebug.Render(renderer);
    }

    /// <summary>
    /// Converts what the physics produced into the pose the drawing side draws from.
    /// </summary>
    /// <remarks>
    /// The heading passes through untouched, and that is a decision rather than an omission. Both
    /// sides measure the same angle the same way — zero is straight forward along the positive
    /// world Y axis, and the angle increases to starboard — so a correction here would turn the
    /// ship the wrong way. Nothing in either type can enforce that agreement, which is why it is
    /// written down here and asserted in <c>GameScreenTests</c> rather than left to be noticed.
    ///
    /// The narrowing to <see cref="float"/> is the world model keeping its precision while the
    /// drawing side takes what a graphics device can use. At the sizes a viewport spans, the
    /// difference is far below a pixel — but that is a statement about <em>precision</em>, and the
    /// narrowing has a <em>range</em> limit too: a coordinate past <see cref="float.MaxValue"/>
    /// becomes an infinity here, and <see cref="ICamera.Target"/> refuses one outright.
    ///
    /// This deliberately gains no guard of its own. It could only throw from the frame loop, which
    /// is too late to help anybody, and the ship cannot fly that far in any amount of time — such
    /// a number only ever arrives fully formed from a save file. It is refused there instead, by
    /// <see cref="SaveGame.CanBeResumed"/>, which is where the range limit is written down.
    /// </remarks>
    static ShipPose PoseOf(Position position, double heading) =>
        new(new Vector2((float)position.X, (float)position.Y), (float)heading);
}
