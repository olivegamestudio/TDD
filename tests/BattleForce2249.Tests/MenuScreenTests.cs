using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class MenuScreenTests
{
    [Fact]
    public void PressingStart_RaisesStartGameRequested()
    {
        Mock<ISaveProgressService> saveProgress = new();
        saveProgress.Setup(x => x.HasProgress()).ReturnsAsync(false);
        
        UIController controller = new();
        MenuScreen menu = new(controller, saveProgress.Object);
        menu.Enter();

        while (!menu.IsReadyForInput)
        {
            menu.Update(TimeSpan.Zero);
        }
        
        bool started = false;
        menu.StartGameRequested += (_, _) => started = true;
        
        menu.Press();
        menu.Release();

        Assert.True(started);
    }

    [Fact]
    public void WithoutAPress_StartGameRequested_DoesNotFire()
    {
        UIController controller = new();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressService>());
        bool started = false;
        menu.StartGameRequested += (_, _) => started = true;

        Assert.False(started);              // no phantom fire on construction
    }

    [Fact]
    public void PressDownAlone_DoesNotRaise()
    {
        // the launch is on RELEASE — a held button has not committed
        UIController controller = new();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressService>());
        bool started = false;
        menu.StartGameRequested += (_, _) => started = true;

        menu.Press();                       // down, never released

        Assert.False(started);
    }

    [Fact]
    public void SecondPress_DoesNotRaiseAgain()
    {
        // OnStartReleased disables the start button — pins the double-fire guard
        UIController controller = new();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressService>());
        int started = 0;
        menu.StartGameRequested += (_, _) => started++;
        menu.Enter();

        while (!menu.IsReadyForInput)
        {
            menu.Update(TimeSpan.Zero);
        }
        
        menu.Press();
        menu.Release();                     // fires + disables start
        menu.Press();
        menu.Release();                     // disabled — must be inert

        Assert.Equal(1, started);
    }

    [Fact]
    public void Enter_FocusesTheStartButton()
    {
        UIController controller = new();
        MenuScreen menu = new(controller, Mock.Of<ISaveProgressService>());
        controller.UnFocus();               // knock focus away, as a reentry might

        menu.Enter();

        Assert.NotNull(controller.Focused);
        Assert.Equal("START", controller.Focused.Name);
    }
}
