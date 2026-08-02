using Microsoft.Extensions.Logging.Abstractions;

namespace OliveGameStudio.Tests;

/// <summary>
/// Boundary cases for the redirect-cycle bound in <see cref="LifecycleScreenDirector.NavigateTo"/> —
/// added by QA while trying to break it, and kept as regression coverage.
/// </summary>
/// <remarks>
/// Two ways to get this wrong, and both are covered here: failing to catch a cycle that is longer
/// than two screens or that closes on a screen part-way along the chain, and catching a "cycle"
/// that is not one — a chain of distinct screens that happen to share a type or a value. None of
/// these carries a safety valve, for the reason the fix's own tests give: against an unbounded
/// director they hang rather than fail, and the bound belongs in the director.
/// </remarks>
public sealed class RedirectCycleAdversarialTests
{
    // ---- cycles the two-screen case would not catch -------------------------

    [Fact]
    public void AThreeScreenCycle_Throws_AndNamesTheWholePathInEntryOrder()
    {
        List<string> log = [];
        ScreenA a = new(log);
        ScreenB b = new(log);
        ScreenC c = new(log);
        a.Redirect = b;
        b.Redirect = c;
        c.Redirect = a;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Navigate(a));

        Assert.Matches(@"ScreenA.* -> .*ScreenB.* -> .*ScreenC.* -> .*ScreenA", error.Message);
    }

    [Fact]
    public void ACycleThatClosesPartWayAlongTheChain_Throws_AndNamesTheScreenItRepeats()
    {
        // A -> B -> C -> B. The repeat is not the screen the navigation started from, so a guard
        // that only compared against the entry point would follow this forever.
        List<string> log = [];
        ScreenA a = new(log);
        ScreenB b = new(log);
        ScreenC c = new(log);
        a.Redirect = b;
        b.Redirect = c;
        c.Redirect = b;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Navigate(a));

        Assert.Matches(@"ScreenA.* -> .*ScreenB.* -> .*ScreenC.* -> .*ScreenB", error.Message);
    }

    [Fact]
    public void EveryScreenEnteredBeforeACycleIsDetected_IsExitedExactlyOnce()
    {
        // Enter/Exit pairing is the invariant a screen's own cleanup depends on. Abandoning the
        // navigation must not strand a screen thinking it is on display, nor exit one twice.
        List<string> log = [];
        ScreenA a = new(log);
        ScreenB b = new(log);
        ScreenC c = new(log);
        a.Redirect = b;
        b.Redirect = c;
        c.Redirect = a;

        Assert.Throws<InvalidOperationException>(() => Navigate(a));

        Assert.Equal(["Enter:A", "Exit:A", "Enter:B", "Exit:B", "Enter:C", "Exit:C"], log);
    }

    [Fact]
    public void ACycleExitsTheScreenTheNavigationStartedFrom_AndDoesNotReEnterIt()
    {
        // The screen that was on display when the doomed navigation began is left exited, not
        // live, and Update ticks nothing afterwards.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        ScreenA home = new(log);
        ScreenB b = new(log);
        ScreenC c = new(log);
        b.Redirect = c;
        c.Redirect = b;

        director.NavigateTo(home);
        Assert.Same(home, director.Current);

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(b));

        Assert.Null(director.Current);
        Assert.Equal(1, log.Count(entry => entry == "Enter:A"));
        Assert.Equal(1, log.Count(entry => entry == "Exit:A"));

        director.Update(TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(log, entry => entry.StartsWith("Update:"));
    }

    [Fact]
    public void AfterACycleHasThrown_TheDirectorStillNavigates()
    {
        // The throw abandons one navigation, it does not wedge the director. A game that catches
        // the exception and falls back to a safe screen has to be able to get there.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        ScreenA a = new(log);
        ScreenB b = new(log);
        a.Redirect = b;
        b.Redirect = a;

        Assert.Throws<InvalidOperationException>(() => director.NavigateTo(a));

        ScreenC safe = new(log);
        director.NavigateTo(safe);

        Assert.Same(safe, director.Current);
    }

    // ---- chains that must NOT be mistaken for cycles ------------------------

    [Fact]
    public void TwoInstancesOfTheSameScreenType_AreTwoDifferentScreens()
    {
        // Comparison is by reference deliberately. A guard that compared types would reject this
        // ordinary chain, and the diagnostic message reports types, so it is worth stating that
        // the *check* does not.
        List<string> log = [];
        ScreenA first = new(log);
        ScreenA second = new(log);
        first.Redirect = second;

        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        director.NavigateTo(first);

        Assert.Same(second, director.Current);
    }

    [Fact]
    public void ScreensThatAreEqualByValue_AreStillTwoDifferentScreens()
    {
        // A screen written as a record is equal to any equal-valued sibling. Tracking entered
        // screens in a plain list and asking Contains — which uses Equals — would see the second
        // gate as already entered and throw on a chain that resolves perfectly well.
        //
        // Two records can only be equal if they hold the same values, so the hops are kept in an
        // itinerary the gates share by reference rather than in a field that would tell them
        // apart. That is contrived, but the collision it provokes is not: the fix comments say in
        // so many words that comparison is by reference because a record screen would otherwise
        // collide with an equal-valued sibling, and this is what that claim is worth.
        Itinerary plan = new();
        ValueScreen firstGate = new("gate", plan);
        ValueScreen secondGate = new("gate", plan);
        ValueScreen destination = new("destination", plan);
        plan.Hops.AddRange([secondGate, destination, null]);

        Assert.Equal(firstGate, secondGate);        // value-equal...
        Assert.NotSame(firstGate, secondGate);      // ...but two screens

        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        director.NavigateTo(firstGate);

        Assert.Same(destination, director.Current);
    }

    [Fact]
    public void ARedirectBackToTheScreenTheNavigationCameFrom_IsNotACycle()
    {
        // Current is not part of this navigation's entered set until it is entered again, and
        // "the save turned out to be unreadable, go back to the title" is an ordinary redirect.
        List<string> log = [];
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        ScreenA home = new(log);
        ScreenB gate = new(log);
        gate.Redirect = home;

        director.NavigateTo(home);
        director.NavigateTo(gate);

        Assert.Same(home, director.Current);
    }

    [Fact]
    public void ALongChainOfDistinctScreens_IsFollowedToItsEnd()
    {
        // Nothing here caps depth; only repetition is rejected. Fifty screens is far past any
        // plausible chain and well past a naive depth limit.
        List<string> log = [];
        List<ScreenA> chain = [.. Enumerable.Range(0, 50).Select(_ => new ScreenA(log))];
        for (int i = 0; i < chain.Count - 1; i++)
        {
            chain[i].Redirect = chain[i + 1];
        }

        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        director.NavigateTo(chain[0]);

        Assert.Same(chain[^1], director.Current);
        Assert.Equal(50, log.Count(entry => entry == "Enter:A"));
    }

    static void Navigate(IScreen screen) =>
        new LifecycleScreenDirector(NullLogger<LifecycleScreenDirector>.Instance).NavigateTo(screen);

    // ---- fakes ----
    // Three distinct types, so a reported path of type names is legible in order.

    abstract class LoggingScreen(string name, List<string> log) : IScreen, IActivatable
    {
        public IScreen? Redirect { get; set; }

        public EnterResult Enter()
        {
            log.Add($"Enter:{name}");
            return Redirect is null ? EnterResult.Stay : EnterResult.RedirectTo(Redirect);
        }

        public void Exit() => log.Add($"Exit:{name}");

        public void Update(TimeSpan frameTime) => log.Add($"Update:{name}");
    }

    sealed class ScreenA(List<string> log) : LoggingScreen("A", log);

    sealed class ScreenB(List<string> log) : LoggingScreen("B", log);

    sealed class ScreenC(List<string> log) : LoggingScreen("C", log);

    /// <summary>
    /// A screen written as a record, so that two of them with the same content are equal by value
    /// while remaining two separate screens.
    /// </summary>
    sealed record ValueScreen(string Name, Itinerary Plan) : IScreen, IActivatable
    {
        public EnterResult Enter() => Plan.Next();

        public void Exit() { }

        public void Update(TimeSpan frameTime) { }
    }

    /// <summary>
    /// The hops of a chain, held outside the screens that walk it so that two screens on the same
    /// itinerary stay equal by value.
    /// </summary>
    sealed class Itinerary
    {
        int _hop;

        public List<IScreen?> Hops { get; } = [];

        public EnterResult Next()
        {
            IScreen? destination = _hop < Hops.Count ? Hops[_hop] : null;
            _hop++;
            return destination is null ? EnterResult.Stay : EnterResult.RedirectTo(destination);
        }
    }
}
