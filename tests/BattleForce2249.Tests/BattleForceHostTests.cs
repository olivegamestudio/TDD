using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class BattleForceHostTests
{
    [Fact]
    public void Boots_OnCompany_ThenReachesTheMenu()
    {
        LifecycleScreenDirector screenDirector = new(NullLogger<LifecycleScreenDirector>.Instance);
        IUIController controller = Mock.Of<IUIController>();
        ISaveProgressService saveProgress = Mock.Of<ISaveProgressService>();
        BattleForceHost game = new BattleForceHost(screenDirector, controller, saveProgress);

        game.Start();
        Assert.IsType<CompanyScreen>(screenDirector.Current);

        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<MenuScreen>(screenDirector.Current);
    }
}
