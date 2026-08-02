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

    // ---- redirect cycles ---------------------------------------------------
    // A redirect chain is a resolution: each screen decides the player belongs
    // somewhere else, until one says stay. Re-entering a screen already entered
    // during the same navigation means it is not resolving, and the director
    // must say so rather than spin. None of these tests carries a safety valve —
    // against an unbounded director they hang rather than fail, which is the
    // point: the bound has to live in the director.

    [Fact]
    public void CyclicRedirect_Throws_RatherThanFollowingItForever()
    {
        // Two screens that redirect at each other. Neither is individually wrong;
        // the cycle only exists in the combination, which is how this reaches a build.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        SecondScreen b = new(log);
        a.RedirectAlwaysTo(b);
        b.RedirectAlwaysTo(a);

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));

        // Stopped at the first repeat: A and B were each entered once, not endlessly.
        Assert.Equal(["Enter:A:begin", "Enter:A:end", "Exit:A", "Enter:B", "Exit:B"], log);
    }

    [Fact]
    public void SelfRedirect_Throws()
    {
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        a.RedirectAlwaysTo(a);

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));
    }

    [Fact]
    public void CycleMessage_NamesTheScreensInTheOrderTheyWereEntered()
    {
        // The whole point of throwing rather than spinning is that the developer
        // is told which screens are arguing, so the path has to be in the message.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        SecondScreen b = new(log);
        a.RedirectAlwaysTo(b);
        b.RedirectAlwaysTo(a);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));

        Assert.Matches(@"RecordingScreen.* -> .*SecondScreen.* -> .*RecordingScreen", error.Message);
    }

    [Fact]
    public void AfterACycle_NothingIsCurrent_AndEveryScreenEnteredHasExited()
    {
        // The navigation landed nowhere, so there is no current screen — Update()
        // must not go on ticking a screen that asked to be redirected away from.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        SecondScreen b = new(log);
        a.RedirectAlwaysTo(b);
        b.RedirectAlwaysTo(a);

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));

        Assert.Null(director.Current);
        Assert.Equal(2, log.Count(entry => entry.StartsWith("Exit:")));   // A and B both exited

        director.Update(TimeSpan.FromSeconds(1));

        Assert.DoesNotContain("Update:A", log);
    }

    [Fact]
    public void AScreenReEnteredWithinOneNavigation_IsRejected_EvenIfItWouldHaveSettled()
    {
        // A bounces to B, B bounces back, and A would stay the second time. This
        // terminates, but it is rejected all the same: at the moment of the repeat
        // it is indistinguishable from a cycle, and whether it settles depends on
        // state mutated inside Enter. One navigation, one entry per screen.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        SecondScreen b = new(log);
        a.RedirectOnceTo(b);
        b.RedirectOnceTo(a);

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));
    }

    [Fact]
    public void TheSameScreen_AcrossSeparateNavigations_IsFine()
    {
        // The guard is per-navigation. Going back to a screen you were on earlier
        // is ordinary, and must not be mistaken for a cycle.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        RecordingScreen a = new("A", log);
        RecordingScreen b = new("B", log);

        director.NavigateTo(a);
        director.NavigateTo(b);
        director.NavigateTo(a);

        Assert.Same(a, director.Current);
    }

    // ---- fakes ----

    sealed class PlainScreen : IScreen
    {
        public void Update(TimeSpan frameTime) { }
    }

    sealed class RecordingScreen(string name, List<string> log) : IScreen, IActivatable
    {
        IScreen? _redirect;
        bool _once;

        public void RedirectOnceTo(IScreen screen)
        {
            _redirect = screen;
            _once = true;
        }

        public void RedirectAlwaysTo(IScreen screen)
        {
            _redirect = screen;
            _once = false;
        }

        public EnterResult Enter()
        {
            log.Add($"Enter:{name}:begin");
            log.Add($"Enter:{name}:end");           // Enter COMPLETES before any Exit may run
            if (_redirect is not null)
            {
                IScreen target = _redirect;
                if (_once)
                {
                    _redirect = null;
                }
                return EnterResult.RedirectTo(target);
            }
            return EnterResult.Stay;
        }

        public void Exit() => log.Add($"Exit:{name}");

        public void Update(TimeSpan frameTime) => log.Add($"Update:{name}");
    }

    /// <summary>
    /// A second screen type, so a reported cycle path is legible. Real screens are distinct
    /// types, and a path of three identical type names would prove nothing about the order.
    /// </summary>
    sealed class SecondScreen(List<string> log) : IScreen, IActivatable
    {
        IScreen? _redirect;
        bool _once;

        public void RedirectOnceTo(IScreen screen)
        {
            _redirect = screen;
            _once = true;
        }

        public void RedirectAlwaysTo(IScreen screen)
        {
            _redirect = screen;
            _once = false;
        }

        public EnterResult Enter()
        {
            log.Add("Enter:B");
            if (_redirect is null)
            {
                return EnterResult.Stay;
            }

            IScreen target = _redirect;
            if (_once)
            {
                _redirect = null;
            }
            return EnterResult.RedirectTo(target);
        }

        public void Exit() => log.Add("Exit:B");

        public void Update(TimeSpan frameTime) => log.Add("Update:B");
    }
}
