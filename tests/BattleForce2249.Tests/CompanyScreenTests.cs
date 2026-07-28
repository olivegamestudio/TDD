using Microsoft.Extensions.Logging.Abstractions;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class CompanyScreenTests
{
    [Fact]
    public void CompanyScreen_IsTheFirstScreenShown()
    {
        IScreenDirector screenDirector = new ScreenDirector(NullLogger<ScreenDirector>.Instance);
        Game game = new Game(screenDirector);
        
        game.Start();

        Assert.IsType<CompanyScreen>(screenDirector.Current);
    } 
    
    [Fact]
    public void Completed_FiresOnce_AfterTheDurationElapses()
    {
        int fires = 0;
        CompanyScreen companyScreen = new(TimeSpan.FromSeconds(1));
        companyScreen.Completed += (_, _) => fires++;
        
        companyScreen.Update(TimeSpan.FromSeconds(1));
        companyScreen.Update(TimeSpan.FromSeconds(1));
        companyScreen.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(1, fires);
    }
}
