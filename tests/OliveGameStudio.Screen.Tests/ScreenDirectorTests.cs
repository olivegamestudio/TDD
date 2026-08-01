using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace OliveGameStudio.Tests;

public sealed class ScreenDirectorTests
{
    class TestScreen : IScreen
    {
        public void Update(TimeSpan frameTime)
        {
        }
    }

    sealed class DrawingScreen : IScreen, IRenderable
    {
        public int Rendered { get; private set; }

        public void Update(TimeSpan frameTime)
        {
        }

        public void Render(IRenderer renderer) => Rendered++;
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

    [Fact]
    public void Draw_RendersTheCurrentScreen_OncePerCall()
    {
        ScreenDirector director = new(NullLogger<ScreenDirector>.Instance);
        DrawingScreen screen = new();
        director.NavigateTo(screen);

        director.Draw(Mock.Of<IRenderer>());
        director.Draw(Mock.Of<IRenderer>());

        Assert.Equal(2, screen.Rendered);
    }

    [Fact]
    public void Draw_SkipsAScreenWithNothingToDraw()
    {
        ScreenDirector director = new(NullLogger<ScreenDirector>.Instance);
        director.NavigateTo(new TestScreen());

        director.Draw(Mock.Of<IRenderer>());
    }

    [Fact]
    public void Draw_DoesNothing_BeforeFirstNavigation()
    {
        // the platform draws every frame, including the ones before anything is on screen
        ScreenDirector director = new(NullLogger<ScreenDirector>.Instance);

        director.Draw(Mock.Of<IRenderer>());
    }
}
