using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Puts a real pilot at the controls: this host's own devices, read by
/// <see cref="DesktopKeyboard"/> and <see cref="DesktopGamePad"/> and handed to the game through
/// <see cref="IHost.Input"/>, with the dead zone this host's hardware wants.
/// </summary>
public static class DesktopPilotServiceCollectionExtensions
{
    /// <summary>
    /// Routes input with this host's stated dead zone rather than the engine's default.
    /// </summary>
    /// <param name="services">The collection to register the router in.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Must be called after <c>AddBattleForce</c>.</strong> The engine registers its own
    /// router with <c>AddSingleton</c>, so the last registration is the one resolved — calling
    /// this first would leave the game running on <see cref="InputRouter.DefaultDeadZone"/> and
    /// say nothing about it.
    /// </para>
    /// <para>
    /// This is all the binding there is left to do. The devices themselves are read by the frame
    /// loop and pushed through <see cref="IHost.Input"/> rather than resolved from the container,
    /// because reading them is a static call into MonoGame with nothing to inject.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddDesktopPilot(this IServiceCollection services) =>
        services.AddSingleton<IInputRouter>(provider => new InputRouter(
            provider.GetRequiredService<IUIController>(),
            provider.GetRequiredService<RoutedShipInput>(),
            DesktopGamePad.DeadZone));
}
