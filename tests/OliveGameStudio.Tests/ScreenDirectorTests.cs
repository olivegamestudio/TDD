using Microsoft.Extensions.Logging.Abstractions;

namespace OliveGameStudio.Tests;

public sealed class ScreenDirectorTests
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
        ScreenDirector director = new(NullLogger<ScreenDirector>.Instance);
        director.NavigateTo(new TestScreen());
        Assert.IsType<TestScreen>(director.Current);
    }
    
    [Fact]
    public void Start_With_Empty_Screen()
    {
        ScreenDirector director = new(NullLogger<ScreenDirector>.Instance);
        Assert.Null(director.Current);
    }
}
