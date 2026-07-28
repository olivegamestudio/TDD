using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class GameTests
{
    [Fact]
    public void Boots_OnCompany_ThenReachesTheMenu()
    {
        LifecycleScreenDirector director = new();
        Game game = new(director);

        game.Start();
        Assert.IsType<CompanyScreen>(director.Current);

        game.Update(TimeSpan.FromDays(1));
        Assert.IsType<MenuScreen>(director.Current);
    }
}
