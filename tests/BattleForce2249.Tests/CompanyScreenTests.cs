using OliveGameStudio;

namespace BattleForce2249.Tests;

public sealed class CompanyScreenTests : HostTestBase
{
    [Fact]
    public void CompanyScreen_IsTheFirstScreenShown()
    {
        IHost game = CreateHost();
        game.Start();
        Assert.IsType<CompanyScreen>(ScreenDirector.Current);
    }

    [Fact]
    public void Completed_DoesNotFire_BeforeTheDurationElapses()
    {
        // the other side of the boundary Completed_FiresOnce crosses
        int fires = 0;
        CompanyScreen companyScreen = new(TimeSpan.FromSeconds(1));
        companyScreen.Completed += (_, _) => fires++;

        companyScreen.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(0, fires);
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

    [Fact]
    public void Completed_FiresAgain_AfterReentry()
    {
        int fires = 0;
        CompanyScreen companyScreen = new(TimeSpan.FromSeconds(1));
        companyScreen.Completed += (_, _) => fires++;

        companyScreen.Enter();
        companyScreen.Update(TimeSpan.FromSeconds(2));      // completes once
        companyScreen.Exit();

        companyScreen.Enter();                              // reenter — countdown resets
        companyScreen.Update(TimeSpan.FromSeconds(2));      // must complete again

        Assert.Equal(2, fires);
    }
}
