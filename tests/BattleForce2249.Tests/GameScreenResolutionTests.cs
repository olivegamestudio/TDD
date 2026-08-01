using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The game screen is built by the container at startup, and it now asks for an
/// <see cref="ILogger{TCategoryName}"/> so a game that fails to begin is heard. A constructor
/// dependency the container cannot satisfy is a crash on launch, not a test failure, so it is
/// worth pinning that the registration the host actually uses can build the screen.
/// </summary>
public class GameScreenResolutionTests
{
    [Fact]
    public void TheHostsRegistration_CanBuildTheGameScreen()
    {
        // exactly what BattleForce2249.MonoGame/Program.cs does: AddLogging, then AddBattleForce
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<GameScreen>(provider.GetRequiredService<IGameScreen>());
    }

    [Fact]
    public void TheWholeHost_CanBeBuilt()
    {
        // the screen is resolved through the host at startup, so resolving it directly is not
        // quite the real path
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHost>());
    }
}
