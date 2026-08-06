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
public sealed class GameScreen(
    IGameSession session,
    IShipInput pilot,
    QuestProximityWatcher questProximity,
    ICamera camera,
    IShipView view,
    StarField stars,
    RegionView region,
    RegionLoader regions)
    : IGameScreen, IActivatable, IRenderable
{
    /// <summary>
    /// The region the game opens in — the collapsing debris field the Disgraced escapes from.
    /// An identifier, never translated.
    /// </summary>
    public const string OpeningRegionId = "debris-field";

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

        Started = session.Continue();
        return EnterResult.Stay;
    }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
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
        ShipMovement ship = session.Ship.Movement;

        // the ship flies first, so the quests are measured against where the player got to this
        // frame rather than where they were at the end of the last one
        ship.Update(session.Player, pilot.Read(), frameTime);

        questProximity.Update(session.Quests, began, session.Player.Position);

        // and last, what the frame produced is handed to the drawing side. The pose is the whole
        // of what the two stages agree about: the physics has no idea a screen exists, and the
        // view has no idea physics does.
        view.Pose = PoseOf(session.Player.Position, ship.Heading);
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
        camera.Orientation = view.Pose.Heading;

        // Before the ship, so the ship is over the stars rather than behind one. It is also what
        // makes the ship look like it is going anywhere: the camera holds the ship still in the
        // middle of the viewport, so the only thing that can move is what is behind it.
        stars.Render(renderer);

        // Between the stars and the ship: the scenery is in the world, so it passes in front of the
        // starfield and behind the thing flying through it.
        region.Render(renderer);

        view.Render(renderer);
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
