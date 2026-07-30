using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class BattleForceHostTests : HostTestBase
{
    [Fact]
    public void Boots_OnCompany_ThenReachesTheMenu()
    {
        IHost game = CreateHost();

        game.Start();
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);

        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<MenuScreen>(ScreenDirector.Current);
    }
}
