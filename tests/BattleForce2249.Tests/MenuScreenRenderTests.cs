using System.Numerics;
using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// What the menu puts on screen. The composition is the point here: five layers in a fixed order,
/// the horizon pinned to the bottom edge rather than filling the window, and a button that says
/// whether it can be pressed and whether it is selected.
/// </summary>
public sealed class MenuScreenRenderTests
{
    static MenuScreen Screen(out UIController controller, bool hasProgress = false)
    {
        Mock<ISaveProgressService> saveProgress = new();
        saveProgress.Setup(x => x.HasProgress()).ReturnsAsync(hasProgress);

        controller = new UIController();
        return new MenuScreen(controller, saveProgress.Object);
    }

    static MenuScreen Ready(out UIController controller)
    {
        MenuScreen menu = Screen(out controller);
        menu.Enter();

        while (!menu.IsReadyForInput)
        {
            menu.Update(TimeSpan.Zero);
        }

        return menu;
    }

    [Fact]
    public void Render_StacksTheLayers_FurthestFirst()
    {
        // The order is the composition. The lone figure stands behind the pair, and the title
        // reads over both — drawn in any other order the menu is still five sprites and looks
        // wrong, which is exactly the kind of thing a list of sprites can catch and an eye may not.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);

        Assert.Equal(
        [
            MenuScreen.HorizonAssetKey,
            MenuScreen.LoneCharacterAssetKey,
            MenuScreen.CharacterPairAssetKey,
            MenuScreen.TitleAssetKey,
            MenuScreen.StartButtonAssetKey,
        ], renderer.Textures.Requested);

        Assert.Equal(5, renderer.Drawn.Count);
    }

    [Fact]
    public void Render_LoadsEachTextureOnce_HoweverManyFramesAreDrawn()
    {
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);
        menu.Render(renderer);
        menu.Render(renderer);

        Assert.Equal(5, renderer.Textures.Requested.Count);
        Assert.Equal(15, renderer.Drawn.Count);
    }

    [Fact]
    public void Render_PinsTheHorizonToTheBottomEdge()
    {
        // The band is 3.4:1 and the window is not, so it is sized to the width and hung from the
        // bottom rather than stretched to fill. Its own bottom edge has to land on the screen's,
        // or the planet floats with a black strip beneath it.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);
        renderer.Textures.SetSize(MenuScreen.HorizonAssetKey, 2748, 800);

        menu.Render(renderer);

        Sprite horizon = renderer.Drawn[0];
        Assert.Equal(new Vector2(960f, 1080f), horizon.Position);
        Assert.Equal(new Vector2(1374f, 800f), horizon.Origin);
        Assert.Equal(1920f / 2748f, horizon.Scale, precision: 5);
    }

    [Fact]
    public void Render_StandsTheCharactersOnTheBottomOfTheScreen()
    {
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);

        menu.Render(renderer);

        foreach (Sprite character in new[] { renderer.Drawn[1], renderer.Drawn[2] })
        {
            Assert.Equal(1080f, character.Position.Y);
            Assert.Equal(960f, character.Position.X);
        }
    }

    [Fact]
    public void Render_DrawsTheLoneFigureLargerThanThePair()
    {
        // It is the nearer read of the two despite standing behind, so it cannot be the smaller.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);

        Assert.True(renderer.Drawn[1].Scale > renderer.Drawn[2].Scale);
    }

    [Fact]
    public void Render_DimsTheStartButton_WhileTheSaveStateIsUnknown()
    {
        // Start is disabled until there is an answer about the save, and the button has to look
        // it. A control that is drawn pressable and is not is worse than one that is drawn off.
        //
        // The save check is held open deliberately. A mock that answers synchronously has the
        // screen ready before the first frame is ever drawn, so the state this test is about would
        // never be seen — which is a property of the mock, not of the screen.
        TaskCompletionSource<bool> stillChecking = new();
        Mock<ISaveProgressService> saveProgress = new();
        saveProgress.Setup(x => x.HasProgress()).Returns(stillChecking.Task);

        MenuScreen menu = new(new UIController(), saveProgress.Object);
        menu.Enter();
        RecordingRenderer renderer = new();

        Assert.False(menu.IsReadyForInput);

        menu.Render(renderer);

        Assert.Equal(MenuScreen.DisabledButtonOpacity, renderer.Drawn[4].Colour.Alpha, precision: 5);
    }

    [Fact]
    public void Render_ShowsTheStartButtonFully_OnceItCanBePressed()
    {
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);

        Assert.Equal(1f, renderer.Drawn[4].Colour.Alpha, precision: 5);
    }

    [Fact]
    public void Render_BrightensTheStartButton_WhileItHoldsFocus()
    {
        // Focus is a tint on the one asset rather than a second texture, so this is the only thing
        // telling a gamepad player which control they are on.
        MenuScreen menu = Ready(out UIController controller);
        RecordingRenderer renderer = new();

        Assert.True(controller.HasFocus);

        menu.Render(renderer);

        Assert.Equal(MenuScreen.FocusedButtonBrightness, renderer.Drawn[4].Colour.Red, precision: 5);
    }
}
