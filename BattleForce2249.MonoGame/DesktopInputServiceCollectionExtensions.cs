using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Binds the desktop's real input devices to the engine's <see cref="IShipInput"/> seam.
/// </summary>
public static class DesktopInputServiceCollectionExtensions
{
    /// <summary>
    /// Puts a player at the controls: a gamepad and the keyboard, whichever is being used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called <em>after</em> <c>AddBattleForce</c>. The engine registers
    /// <see cref="NeutralShipInput"/> with <c>AddSingleton</c>, so the last registration is the
    /// one resolved — that ordering is the seam, and calling this first would silently leave the
    /// game with nobody flying it.
    /// </para>
    /// <para>
    /// The gamepad is asked first. A player holding a controller is deliberately using it, and a
    /// resting pad is zeroed by its dead zone, so asking it first costs the keyboard nothing.
    /// </para>
    /// </remarks>
    /// <param name="services">The collection to bind the devices in.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddDesktopPilot(this IServiceCollection services) =>
        services.AddSingleton<IShipInput>(
            new FirstActiveShipInput(new GamePadShipInput(), new KeyboardShipInput()));
}
