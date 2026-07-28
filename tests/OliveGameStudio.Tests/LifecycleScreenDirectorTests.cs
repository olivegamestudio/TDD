using Castle.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OliveGameStudio.Tests;

public sealed class LifecycleScreenDirectorTests
{
    class TestScreen : IScreen
    {
        public void Update(TimeSpan frameTime)
        {
        }
    }
    
    [Fact]
    public void Navigate_Sets_CurrentScreen()
    {
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        director.NavigateTo(new TestScreen());
        Assert.IsType<TestScreen>(director.Current);
    }
    
    [Fact]
    public void Start_With_Empty_Screen()
    {
        LifecycleScreenDirector director = new(NullLogger<LifecycleScreenDirector>.Instance);
        Assert.Null(director.Current);
    }
}
