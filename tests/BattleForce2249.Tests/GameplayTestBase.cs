using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Drives the shipping composition the way a player does: through the company screen, a real press
/// of the menu's start button, and then frame after frame of the game screen.
/// </summary>
/// <remarks>
/// A rendered playthrough is not the only playthrough. Everything that decides whether a quest can
/// be played — where the player is, what the pilot asks for, what the markers fire — is logic, and
/// this base drives all of it without a window. Tests that reach in and move the player belong to
/// whatever they are testing; tests that ask "can this be played" fly the ship.
/// </remarks>
public abstract class GameplayTestBase : HostTestBase
{
    /// <summary>
    /// One frame at 60Hz, the rate the game is played at.
    /// </summary>
    protected static readonly TimeSpan Frame = TimeSpan.FromSeconds(1 / 60.0);

    /// <summary>
    /// A pilot holding full thrust and no helm — flying straight forward.
    /// </summary>
    protected static readonly ShipControls FullAhead = new(thrust: 1, turn: 0);

    /// <summary>
    /// Gets the game in progress.
    /// </summary>
    protected IGameSession Session => Resolve<IGameSession>();

    /// <summary>
    /// Gets the screen the game is played on.
    /// </summary>
    protected IGameScreen GameScreen => Resolve<IGameScreen>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    /// <param name="configure">Anything else the test needs registered, such as a pilot.</param>
    /// <returns>The running host, ready to be played.</returns>
    protected IHost StartTheGame(Action<IServiceCollection>? configure = null)
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

    /// <summary>
    /// Runs the game for a stretch of 60Hz frames, stopping early once <paramref name="until"/> is
    /// satisfied so a test that is waiting for something does not have to guess how long it takes.
    /// </summary>
    /// <param name="host">The running host.</param>
    /// <param name="frames">The most frames to play.</param>
    /// <param name="until">The condition to stop on, or <c>null</c> to play every frame.</param>
    protected static void Play(IHost host, int frames, Func<bool>? until = null)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            if (until?.Invoke() is true)
            {
                return;
            }

            host.Update(Frame);
        }
    }

    /// <summary>
    /// Plays until <paramref name="until"/> is satisfied, and reports whether it ever was.
    /// </summary>
    /// <remarks>
    /// The frame budget is a guard against a test hanging when something is unplayable, not a
    /// measure of how long a quest should take — a caller that cares about the pace of a quest
    /// should say so with its own assertion.
    /// </remarks>
    /// <param name="host">The running host.</param>
    /// <param name="until">The condition being waited for.</param>
    /// <param name="frames">The most frames to play before giving up.</param>
    /// <returns><c>true</c> if the condition was reached within the budget.</returns>
    protected static bool PlayUntil(IHost host, Func<bool> until, int frames = 60 * 60)
    {
        Play(host, frames, until);

        return until();
    }
}
