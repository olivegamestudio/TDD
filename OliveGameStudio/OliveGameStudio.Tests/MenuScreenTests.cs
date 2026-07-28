namespace OliveGameStudio.Tests;

public sealed class MenuScreenTests
{
    [Fact]
    public void MenuScreen_IsTheFirstScreenShown()
    {
        IScreenDirector screenDirector = new ScreenDirector();
        Game game = new Game(screenDirector);
        
        game.Start();
        game.Update(TimeSpan.FromSeconds(2));

        Assert.IsType<MenuScreen>(screenDirector.Current);
    }
}
