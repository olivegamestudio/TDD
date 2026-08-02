using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Covers the one thing about the desktop pilot that can be checked without a device: that the
/// host composes to it rather than to the engine's default, and that it stops composing to it if
/// the registration ever moves back above <c>AddBattleForce</c>.
/// </summary>
public sealed class DesktopPilotRegistrationTests
{
    static IShipInput Resolve(Action<IServiceCollection> compose)
    {
        ServiceCollection services = new();
        services.AddLogging();

        compose(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IShipInput>();
    }

    [Fact]
    public void TheHost_FliesOnItsOwnDevices_RatherThanOnTheEnginesDefault()
    {
        IShipInput input = Resolve(services =>
        {
            services.AddBattleForce();
            services.AddDesktopPilot();
        });

        Assert.IsType<FirstActiveShipInput>(input);
    }

    [Fact]
    public void TheGamepadIsAskedBeforeTheKeyboard()
    {
        // order is the whole of the arbitration policy, so it is pinned rather than left to the
        // order the devices happen to appear in a constructor call
        FirstActiveShipInput input = (FirstActiveShipInput)Resolve(services =>
        {
            services.AddBattleForce();
            services.AddDesktopPilot();
        });

        Assert.Collection(
            input.Devices,
            device => Assert.IsType<GamePadShipInput>(device),
            device => Assert.IsType<KeyboardShipInput>(device));
    }

    [Fact]
    public void RegisteringBeforeAddBattleForce_LeavesTheGameUnflyable()
    {
        // not a wish for this ordering but a guard on the one above: the engine registers its
        // default with AddSingleton, so a tidy-up that moved AddDesktopPilot up the composition
        // root would silently give the shipping game nobody at the controls
        IShipInput input = Resolve(services =>
        {
            services.AddDesktopPilot();
            services.AddBattleForce();
        });

        Assert.IsType<NeutralShipInput>(input);
    }
}
