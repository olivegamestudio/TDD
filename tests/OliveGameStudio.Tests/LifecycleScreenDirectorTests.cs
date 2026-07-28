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
        LifecycleScreenDirector director = new();
        director.NavigateTo(new TestScreen());
        Assert.IsType<TestScreen>(director.Current);
    }
    
    [Fact]
    public void Start_With_Empty_Screen()
    {
        LifecycleScreenDirector director = new();
        Assert.Null(director.Current);
    }
}
