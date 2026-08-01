using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A host taken to the point where the game is actually being played, and driven a frame at a
/// time from there. Tests that need to watch gameplay happen — quests starting, quests finishing —
/// build on this rather than each assembling the same run-up.
/// </summary>
/// <remarks>
/// <para>
/// The run-up is the shipping one: the company screen elapses, the menu's start button is really
/// pressed, and the screen director enters the game screen. Nothing here reaches past a screen to
/// set up the state it wants, because a game that only works when a test arranges it is not a
/// game that works.
/// </para>
/// <para>
/// Movement is the exception, and it is a temporary one. Nothing in the game moves the ship yet —
/// control input and physics are issue #3 — so <see cref="FlyTowards"/> stands in for the pilot
/// by stepping the player along a straight line each frame. When the movement system lands, that
/// method is the one place to put a real pilot behind, and every test built on it keeps its
/// meaning.
/// </para>
/// </remarks>
public abstract class GameplayTestBase : HostTestBase
{
    /// <summary>
    /// One frame at sixty a second, the rate the shipping game updates at.
    /// </summary>
    protected static readonly TimeSpan Frame = TimeSpan.FromSeconds(1 / 60.0);

    /// <summary>
    /// How far the player covers in a frame while flying. Ten units a frame is 600 a second, the
    /// order of speed a ship crossing a thousand-unit debris field wants, and coarse enough that a
    /// trigger too tight to catch a moving ship fails these tests rather than passing them.
    /// </summary>
    protected const double UnitsPerFrame = 10;

    /// <summary>
    /// How many frames a leg of a flight is given before it is called unplayable. A minute of
    /// flying covers 36,000 units, a long way past anything the campaign has authored.
    /// </summary>
    protected const int FramesPerLeg = 60 * 60;

    /// <summary>
    /// Gets the game in progress.
    /// </summary>
    protected IGameSession Session => Resolve<IGameSession>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    /// <param name="services">
    /// Test doubles to register after the shipping composition, so they win over it.
    /// </param>
    /// <returns>The running host, ready to be given frames.</returns>
    protected IHost StartTheGame(Action<IServiceCollection>? services = null)
    {
        Configure(services: collection =>
        {
            // the real UI controller, so pressing the start button raises its action
            collection.AddSingleton<IUIController, UIController>();
            // the shipping director, so navigating a screen actually enters it
            collection.AddSingleton<IScreenDirector, LifecycleScreenDirector>();

            services?.Invoke(collection);
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

    /// <summary>
    /// Gives the host frames with the player sitting still.
    /// </summary>
    /// <param name="host">The running host.</param>
    /// <param name="frames">How many frames to play.</param>
    protected static void Play(IHost host, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            host.Update(Frame);
        }
    }

    /// <summary>
    /// Flies the player straight at <paramref name="target"/>, a frame at a time, until
    /// <paramref name="arrived"/> is satisfied or the frames run out.
    /// </summary>
    /// <remarks>
    /// The player is moved before the frame rather than after it, so the frame the game sees is
    /// the position the player is actually in. Flying stops at the target rather than overshooting
    /// it, so a trigger that never fires is a trigger that never fires rather than a step size that
    /// stepped over it.
    /// </remarks>
    /// <param name="host">The running host.</param>
    /// <param name="target">Where to fly.</param>
    /// <param name="arrived">What the flight is waiting for.</param>
    /// <param name="maxFrames">How many frames to give it before giving up.</param>
    /// <returns><c>true</c> when <paramref name="arrived"/> was satisfied.</returns>
    protected bool FlyTowards(IHost host, Position target, Func<bool> arrived, int maxFrames = FramesPerLeg)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (arrived())
            {
                return true;
            }

            Position position = Session.Player.Position;
            double remaining = position.DistanceTo(target);
            if (remaining > 0)
            {
                double step = Math.Min(UnitsPerFrame, remaining);
                Session.Player.MoveBy(
                    (target.X - position.X) / remaining * step,
                    (target.Y - position.Y) / remaining * step);
            }

            host.Update(Frame);
        }

        return arrived();
    }
}
