namespace OliveGameStudio.Tests;

public sealed class CompanyScreenTests
{
    [Fact]
    public void CompanyScreen_IsTheFirstScreenShown()
    {
        IScreenDirector screenDirector = new ScreenDirector();
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
