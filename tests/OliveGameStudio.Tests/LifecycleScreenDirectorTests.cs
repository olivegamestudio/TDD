using Microsoft.Extensions.Logging.Abstractions;

namespace OliveGameStudio.Tests;

public sealed class LifecycleScreenDirectorTests
{
    [Fact]
    public void Current_IsNull_BeforeFirstNavigation()
    {
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);

        Assert.Null(director.Current);
    }

    [Fact]
    public void Navigate_Sets_CurrentScreen()
    {
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);

        director.NavigateTo(new PlainScreen());

        Assert.IsType<PlainScreen>(director.Current);
    }

    [Fact]
    public void Navigate_ExitsTheOld_ThenEntersTheNew()
    {
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        RecordingScreen b = new("B", log);

        director.NavigateTo(a);
        director.NavigateTo(b);

        Assert.Equal(["Enter:A:begin", "Enter:A:end", "Exit:A", "Enter:B:begin", "Enter:B:end"], log);
        Assert.Same(b, director.Current);
    }

    [Fact]
    public void Enter_Stay_DoesNotLoop()
    {
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);

        director.NavigateTo(a);

        Assert.Equal(["Enter:A:begin", "Enter:A:end"], log);
        Assert.Same(a, director.Current);
    }

    [Fact]
    public void RedirectChain_LandsOnTheFinalScreen_WithCleanPairing()
    {
        // Every Enter runs to completion BEFORE its Exit — the reentrant
        // interleave (Exit-during-Enter) is unrepresentable by construction.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        RecordingScreen b = new("B", log);
        RecordingScreen c = new("C", log);
        a.RedirectOnceTo(b);                // A's Enter says: go to B
        b.RedirectOnceTo(c);                // B's Enter says: go to C

        director.NavigateTo(a);

        Assert.Equal([
            "Enter:A:begin", "Enter:A:end",
            "Exit:A",
            "Enter:B:begin", "Enter:B:end",
            "Exit:B",
            "Enter:C:begin", "Enter:C:end",
        ], log);
        Assert.Same(c, director.Current);
    }

    [Fact]
    public void NonActivatableScreen_JustBecomesCurrent()
    {
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        PlainScreen plain = new();

        director.NavigateTo(plain);         // no IActivatable — treated as Stay

        Assert.Same(plain, director.Current);
    }

    [Fact]
    public void Update_TicksTheCurrentScreen()
    {
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        director.NavigateTo(a);

        director.Update(TimeSpan.FromSeconds(1));

        Assert.Contains("Update:A", log);
    }

    // ---- fakes ----

    sealed class PlainScreen : IScreen
    {
        public void Update(TimeSpan frameTime) { }
    }

    sealed class RecordingScreen(string name, List<string> log) : IScreen, IActivatable
    {
        IScreen? _redirect;

        public void RedirectOnceTo(IScreen screen) => _redirect = screen;

        public EnterResult Enter()
        {
            log.Add($"Enter:{name}:begin");
            log.Add($"Enter:{name}:end");           // Enter COMPLETES before any Exit may run
            if (_redirect is not null)
            {
                IScreen target = _redirect;
                _redirect = null;
                return EnterResult.RedirectTo(target);
            }
            return EnterResult.Stay;
        }

        public void Exit() => log.Add($"Exit:{name}");

        public void Update(TimeSpan frameTime) => log.Add($"Update:{name}");
    }
}
