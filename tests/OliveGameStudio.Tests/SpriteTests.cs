using System.Numerics;

namespace OliveGameStudio.Tests;

/// <summary>
/// What a sprite carries. The colour is the part with a default, and a default that came out
/// transparent would draw nothing at all — every existing caller builds a sprite without
/// mentioning one.
/// </summary>
public sealed class SpriteTests
{
    static Sprite Create() => new(new StubTexture(), new Vector2(10f, 20f), 0.5f, Vector2.Zero, 2f);

    [Fact]
    public void IsOpaqueWhite_UntilSomethingSaysOtherwise()
    {
        Assert.Equal(Colour.White, Create().Colour);
    }

    [Fact]
    public void KeepsTheColourItWasGiven()
    {
        Colour half = Colour.White.WithAlpha(0.5f);

        Sprite sprite = Create() with { Colour = half };

        Assert.Equal(half, sprite.Colour);
    }

    [Fact]
    public void KeepsItsColourWhenSomethingElseIsChanged()
    {
        // `with` copies the sprite; a colour held outside the positional parameters has to be
        // carried across with the rest of it.
        Sprite tinted = Create() with { Colour = new Colour(1f, 0f, 0f, 1f) };

        Sprite moved = tinted with { Position = new Vector2(1f, 2f) };

        Assert.Equal(new Colour(1f, 0f, 0f, 1f), moved.Colour);
    }

    [Fact]
    public void SpritesDifferingOnlyInColour_AreNotEqual()
    {
        Sprite sprite = Create();

        Assert.NotEqual(sprite, sprite with { Colour = Colour.White.WithAlpha(0.5f) });
    }

    sealed class StubTexture : ITexture
    {
        public int Width => 32;

        public int Height => 32;

        public void Dispose()
        {
        }
    }
}
