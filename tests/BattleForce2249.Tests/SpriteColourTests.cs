using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The colour a sprite is given is the colour that reaches the renderer, and the colour the
/// drawables in the game ask for when they say nothing is opaque white. The renderer these run
/// through records rather than draws, which is the only way this is assertable at all — the real
/// one needs a graphics device and the result of it needs an eye.
/// </summary>
public sealed class SpriteColourTests
{
    [Fact]
    public void ADrawnSpriteReachesTheRendererWithItsColour()
    {
        RecordingRenderer renderer = new();
        ITexture texture = renderer.Textures.Load("anything");
        Colour halfFadedRed = new(1f, 0f, 0f, 0.5f);

        renderer.Draw(new Sprite(texture, Vector2.Zero, 0f, Vector2.Zero, 1f)
        {
            Colour = halfFadedRed,
        });

        Assert.Equal(halfFadedRed, renderer.Single().Colour);
    }

    [Fact]
    public void TheShipIsDrawnOpaqueWhite()
    {
        // nothing tints the ship yet, and a drawable that quietly picked up a colour would be
        // invisible or the wrong shade with nothing in the code saying so.
        ShipView view = new(new Camera2D());
        RecordingRenderer renderer = new();

        view.Render(renderer);

        Assert.Equal(Colour.White, renderer.Single().Colour);
    }

    [Fact]
    public void TheStarsAreDrawnOpaqueWhite()
    {
        StarField field = new(new Camera2D());
        RecordingRenderer renderer = new();

        field.Render(renderer);

        Assert.All(renderer.Drawn, sprite => Assert.Equal(Colour.White, sprite.Colour));
        Assert.NotEmpty(renderer.Drawn);
    }
}
