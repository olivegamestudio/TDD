namespace OliveGameStudio.Tests;

/// <summary>
/// What a colour is worth on its own, before anything is drawn with it. The channel order and
/// the refusal of a channel that is not a number are the two things a renderer cannot check for
/// itself.
/// </summary>
public sealed class ColourTests
{
    [Fact]
    public void White_IsEveryChannelFull_AndFullyOpaque()
    {
        Colour white = Colour.White;

        Assert.Equal(1f, white.Red);
        Assert.Equal(1f, white.Green);
        Assert.Equal(1f, white.Blue);
        Assert.Equal(1f, white.Alpha);
    }

    [Fact]
    public void Channels_AreKeptInTheOrderTheyWereGiven()
    {
        // red, green, blue, alpha — a colour built with them transposed is a colour nobody can
        // see is wrong until it is on screen.
        Colour colour = new(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.Equal(0.1f, colour.Red);
        Assert.Equal(0.2f, colour.Green);
        Assert.Equal(0.3f, colour.Blue);
        Assert.Equal(0.4f, colour.Alpha);
    }

    [Fact]
    public void WithAlpha_KeepsTheColourAndChangesOnlyHowMuchOfItShows()
    {
        Colour colour = new(0.2f, 0.4f, 0.6f, 1f);

        Colour faded = colour.WithAlpha(0.25f);

        Assert.Equal(new Colour(0.2f, 0.4f, 0.6f, 0.25f), faded);
    }

    [Fact]
    public void Colours_AreEqualWhenEveryChannelIs()
    {
        Assert.Equal(new Colour(0.1f, 0.2f, 0.3f, 0.4f), new Colour(0.1f, 0.2f, 0.3f, 0.4f));
        Assert.NotEqual(new Colour(0.1f, 0.2f, 0.3f, 0.4f), new Colour(0.1f, 0.2f, 0.3f, 1f));
    }

    [Theory]
    [InlineData(float.NaN, 1f, 1f, 1f)]
    [InlineData(1f, float.NaN, 1f, 1f)]
    [InlineData(1f, 1f, float.NaN, 1f)]
    [InlineData(1f, 1f, 1f, float.NaN)]
    [InlineData(float.PositiveInfinity, 1f, 1f, 1f)]
    [InlineData(1f, float.NegativeInfinity, 1f, 1f)]
    [InlineData(1f, 1f, float.PositiveInfinity, 1f)]
    [InlineData(1f, 1f, 1f, float.PositiveInfinity)]
    public void RefusesAChannelThatIsNotANumber(float red, float green, float blue, float alpha)
    {
        // the same reason Camera2D refuses one: a channel that is not a number reaches the
        // graphics device as whatever the conversion happens to produce, draws without an error,
        // and names neither the sprite nor the frame that produced it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Colour(red, green, blue, alpha));
    }

    [Fact]
    public void WithAlpha_RefusesAnAlphaThatIsNotANumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Colour.White.WithAlpha(float.NaN));
    }
}
