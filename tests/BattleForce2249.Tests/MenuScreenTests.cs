using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class MenuScreenTests
{
    [Fact]
    public void PressingStart_RaisesStartGameRequested()
    {
        IUIController controller = new UIController();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressController>());
        
        bool started = false;
        menu.StartGameRequested += (_, _) => started = true;

        menu.Press();
        menu.Release();

        Assert.True(started);
    }

    [Fact]
    public void WithoutAPress_StartGameRequested_DoesNotFire()
    {
        IUIController controller = new UIController();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressController>());
        bool started = false;
        menu.StartGameRequested += (_, _) => started = true;

        Assert.False(started);              // no phantom fire on construction
    }
}
