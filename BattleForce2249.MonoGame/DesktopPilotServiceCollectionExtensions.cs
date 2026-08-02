using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Puts a real pilot at the controls: the desktop input devices this host owns, bound to the
/// engine's <see cref="IShipInput"/> seam.
/// </summary>
public static class DesktopPilotServiceCollectionExtensions
{
    /// <summary>
    /// Binds the keyboard and the gamepad to <see cref="IShipInput"/>.
    /// </summary>
    /// <param name="services">The collection to bind the devices in.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Must be called after <c>AddBattleForce</c>.</strong> The engine registers
    /// <see cref="NeutralShipInput"/> as its default with <c>AddSingleton</c>, so the last
    /// registration is the one resolved — calling this first would leave the game unflyable and
    /// say nothing about it.
    /// </para>
    /// <para>
    /// The gamepad is asked before the keyboard. A resting pad is zeroed by its dead zone, so
    /// asking it first costs the keyboard nothing, while a player who has picked up a pad has
    /// plainly chosen it.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddDesktopPilot(this IServiceCollection services) =>
        services.AddSingleton<IShipInput>(
            new FirstActiveShipInput(new GamePadShipInput(), new KeyboardShipInput()));
}
