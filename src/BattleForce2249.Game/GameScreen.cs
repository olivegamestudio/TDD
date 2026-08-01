using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The screen the game is played on. It composes what is in the world and draws it; what moves
/// the ship belongs elsewhere and reaches the screen through <see cref="IShipView.Pose"/>.
/// </summary>
/// <param name="camera">The camera the world is drawn through.</param>
/// <param name="ship">The player's ship.</param>
public sealed class GameScreen(ICamera camera, IShipView ship) : IGameScreen, IRenderable
{
    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        // The camera follows the ship rather than standing still. At the speeds this ship flies,
        // a fixed viewport is left behind within seconds of the player touching the throttle,
        // and a ship that has flown off the edge of the screen is indistinguishable from one
        // that was never drawn. The world moves; the ship holds the middle.
        camera.Target = ship.Pose.Position;

        ship.Render(renderer);
    }
}
