using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Covers the composition root's half of the input seam: that the desktop host actually wins over
/// the engine's default, and in the order it has to be called in to do so.
/// </summary>
/// <remarks>
/// What is deliberately not covered here is reading a real device. <c>Keyboard.GetState</c> and
/// <c>GamePad.GetState</c> are static calls into MonoGame with no seam to stand in front of them,
/// so nothing below invokes <see cref="IShipInput.Read"/>. The mapping those calls feed —
/// <see cref="ShipControls.FromKeys"/>, <see cref="ShipControls.FromStick"/> and
/// <see cref="FirstActiveShipInput"/> — is engine code and is covered in
/// <c>OliveGameStudio.World.Tests</c>. What is left untested is the few lines that name the keys
/// and the stick axes.
/// </remarks>
public sealed class DesktopPilotRegistrationTests
{
    static ServiceProvider BuildHost() =>
        new ServiceCollection()
            .AddBattleForce()
            .AddDesktopPilot()
            .BuildServiceProvider();

    [Fact]
    public void TheHostPutsAPlayerAtTheControls_RatherThanTheEnginesDefault()
    {
        // without this the shipping game launches and the ship sits at the start marker forever
        using ServiceProvider provider = BuildHost();

        Assert.IsType<FirstActiveShipInput>(provider.GetRequiredService<IShipInput>());
    }

    [Fact]
    public void CalledBeforeAddBattleForce_TheEnginesDefaultWinsInstead()
    {
        // pins the ordering the seam depends on, so a reordering of the composition root fails
        // here rather than as a game nobody can fly
        using ServiceProvider provider = new ServiceCollection()
            .AddDesktopPilot()
            .AddBattleForce()
            .BuildServiceProvider();

        Assert.IsType<NeutralShipInput>(provider.GetRequiredService<IShipInput>());
    }

    [Fact]
    public void TheSameControlsAreSharedByEverythingThatReadsThem()
    {
        // two pilots would each poll the devices; the game screen reads one
        using ServiceProvider provider = BuildHost();

        Assert.Same(provider.GetRequiredService<IShipInput>(), provider.GetRequiredService<IShipInput>());
    }

    [Fact]
    public void BothTheGamePadAndTheKeyboardAreBound_TheGamePadFirst()
    {
        // the design canon says desktop, keyboard and gamepad — both, not one. The order is a
        // decision too: a player holding a controller is deliberately using it
        using ServiceProvider provider = BuildHost();
        FirstActiveShipInput pilot = (FirstActiveShipInput)provider.GetRequiredService<IShipInput>();

        Assert.Collection(
            pilot.Devices,
            device => Assert.IsType<GamePadShipInput>(device),
            device => Assert.IsType<KeyboardShipInput>(device));
    }
}
