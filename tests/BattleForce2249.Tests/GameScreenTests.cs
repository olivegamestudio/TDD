using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game screen as the seam between the screen lifecycle and the quest session:
/// entering the screen begins the game, and each frame advances it.
/// </summary>
public sealed class GameScreenTests : HostTestBase
{
    IGameSession Session => Resolve<IGameSession>();

    IGameScreen GameScreen => Resolve<IGameScreen>();

    /// <summary>
    /// Drives the host through the company screen and a real press of the menu's start button,
    /// leaving the game screen active — the same path a player takes on a new game launch.
    /// </summary>
    IHost StartTheGame()
    {
        Configure(services: services => services
            // the real UI controller, so pressing the start button raises its action
            .AddSingleton<IUIController, UIController>()
            // the shipping director, so navigating a screen actually enters it
            .AddSingleton<IScreenDirector, LifecycleScreenDirector>());

        IHost host = CreateHost();
        host.Start();
        host.Update(TimeSpan.FromDays(1));                      // company screen elapses

        MenuScreen menu = (MenuScreen)Resolve<IMenuScreen>();
        for (int frame = 0; !menu.IsReadyForInput; frame++)
        {
            Assert.True(frame < 1000, "the menu never became ready for input");
            host.Update(TimeSpan.Zero);
        }

        menu.Press();
        menu.Release();

        return host;
    }

    [Fact]
    public void EnteringTheScreen_StartsTheGame()
    {
        ((IActivatable)GameScreen).Enter();

        Assert.True(Session.IsReady);
    }

    [Fact]
    public void EnteringTheScreen_AutoStartsQuest1()
    {
        ((IActivatable)GameScreen).Enter();

        Quest quest = Assert.Single(Session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
    }

    [Fact]
    public void TheGameScreenIsActive_OnceTheMenuRequestsAStart()
    {
        StartTheGame();

        Assert.Same(GameScreen, ScreenDirector.Current);
    }

    [Fact]
    public void Quest1_IsActive_OnANewGameLaunch()
    {
        StartTheGame();

        Quest quest = Assert.Single(Session.Quests.Active);
        Assert.Equal(BattleForceCampaign.Quest1Id, quest.Id);
        Assert.Equal("Get out. The debris field is collapsing around you.", quest.Title);
    }

    [Fact]
    public void MovingForwardAcrossFrames_CompletesQuest1()
    {
        IHost host = StartTheGame();
        QuestDefinition quest1 = Resolve<ICampaign>().Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

        // travel to the end marker a frame at a time, as the movement system will
        for (int frame = 0; !quest1.End.IsReachedBy(Session.Player.Position); frame++)
        {
            Assert.True(frame < 10_000, "the player never reached the end marker");
            Session.Player.MoveBy(0, 10);
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }

        Assert.Single(Session.Quests.Completed);
        Assert.Empty(Session.Quests.Active);
    }

    [Fact]
    public void Quest1_StaysInProgress_UntilTheEndMarkerIsReached()
    {
        IHost host = StartTheGame();

        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.Single(Session.Quests.Active);
        Assert.Empty(Session.Quests.Completed);
    }
}
