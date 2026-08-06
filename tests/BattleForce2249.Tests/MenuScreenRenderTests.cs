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
        // The order is the composition, and this asserts the order things were *drawn* rather than
        // the order they were loaded — they differ, and only the first of the two is what the
        // player sees. The horizon lands over both figures on purpose, so the planet cuts across
        // their feet; drawn the other way round they stand on top of it and the scene falls flat.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);

        Assert.Equal(
        [
            MenuScreen.LoneCharacterAssetKey,
            MenuScreen.CharacterPairAssetKey,
            MenuScreen.HorizonAssetKey,
            MenuScreen.TitleAssetKey,
            MenuScreen.StartButtonAssetKey,
        ], renderer.Drawn.Select(sprite => KeyOf(renderer, sprite)));
    }

    /// <summary>
    /// Which asset key a drawn sprite came from. The loader hands out one texture per key, so
    /// identity is enough to name it.
    /// </summary>
    static string KeyOf(RecordingRenderer renderer, Sprite sprite)
    {
        string[] keys =
        [
            MenuScreen.HorizonAssetKey,
            MenuScreen.LoneCharacterAssetKey,
            MenuScreen.CharacterPairAssetKey,
            MenuScreen.TitleAssetKey,
            MenuScreen.StartButtonAssetKey,
        ];

        return keys.Single(key => ReferenceEquals(renderer.Textures.Load(key), sprite.Texture));
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

        Sprite horizon = renderer.Drawn[2];
        Assert.Equal(new Vector2(960f, 1080f), horizon.Position);
        Assert.Equal(new Vector2(1374f, 800f), horizon.Origin);
        Assert.Equal(1920f / 2748f, horizon.Scale, precision: 5);
    }

    [Fact]
    public void Render_StandsTheCharactersOnTheirBaselines()
    {
        // Lifted clear of the bottom edge rather than stood on it. The horizon is drawn over them,
        // so where the baseline sits decides how much of each figure the planet cuts across.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);

        menu.Render(renderer);

        Assert.Equal(960f, renderer.Drawn[0].Position.X);
        Assert.Equal(960f, renderer.Drawn[1].Position.X);

        Assert.Equal(1080f * MenuScreen.LoneCharacterBaselineFraction, renderer.Drawn[0].Position.Y, precision: 3);
        Assert.Equal(1080f * MenuScreen.CharacterPairBaselineFraction, renderer.Drawn[1].Position.Y, precision: 3);
    }

    [Fact]
    public void Render_RaisesTheLoneFiguresHead_AboveThePair()
    {
        // He is the one meant to dominate, and it is the top of him that says so — his head has to
        // clear theirs. Comparing the baselines would say the opposite and be right about the
        // wrong thing: he is anchored *lower* than the pair precisely so more of him falls off the
        // bottom, and it is his greater height that carries his head above them.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);

        menu.Render(renderer);

        Assert.True(TopOf(renderer.Drawn[0]) < TopOf(renderer.Drawn[1]));
    }

    /// <summary>
    /// Where the top edge of a drawn sprite lands. A smaller Y is higher up: the screen's Y axis
    /// points down.
    /// </summary>
    static float TopOf(Sprite sprite) => sprite.Position.Y - (sprite.Origin.Y * sprite.Scale);

    [Fact]
    public void Render_CropsTheFigures_RatherThanShrinkingThemToFit()
    {
        // Both stand taller than the window and their feet land past its bottom edge. A figure
        // scaled until it fits entirely on screen is a small figure, and the frame is meant to be
        // full of them.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new(width: 1920f, height: 1080f);

        menu.Render(renderer);

        Assert.True(renderer.Drawn[0].Position.Y > 1080f);
        Assert.True(renderer.Drawn[1].Position.Y > 1080f);
    }

    [Fact]
    public void Render_DrawsTheLoneFigureLargerThanThePair()
    {
        // It is the nearer read of the two despite standing behind, so it cannot be the smaller.
        MenuScreen menu = Ready(out _);
        RecordingRenderer renderer = new();

        menu.Render(renderer);

        Assert.True(renderer.Drawn[0].Scale > renderer.Drawn[1].Scale);
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
