using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The seam between the two crossings, probed from both sides of it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StarFieldTests"/> covers each branch well inside its own territory: an absurd star
/// against the shipping declaration, and an ordinary star against a 100,000-pixel one. Neither
/// goes near the point where the two meet, and that point is where this change can be wrong
/// without any existing test noticing — the whole defect being fixed is a branch that names the
/// wrong thing, so a branch that turns over one step early or late names the wrong thing too.
/// </para>
/// <para>
/// The floor is <c>(widest + 2·size) / 252</c> and the ceiling is <c>narrowest / 2</c>, so the
/// declaration crosses them on its own exactly when <c>widest &gt; 126 · narrowest</c>. These sit
/// on that line, one step either side of it, and check the harder question underneath: that
/// whichever field the message names is a field the reader can actually move.
/// </para>
/// </remarks>
public sealed class StarFieldDeclarationAdversarialTests
{
    /// <summary>The point where the floor under a star of no size meets the ceiling exactly.</summary>
    /// <remarks>
    /// 90,720 is 126 × 720, and both halves divide exactly in single precision, so this really is
    /// the boundary rather than somewhere near it — the test asserts that before it relies on it.
    /// </remarks>
    const float WidestThatExactlyMeetsTheCeiling = 90_720f;

    const float Narrowest = 720f;

    static IOptions<DisplayOptions> Declaring(float widestInPixels, float narrowestInPixels) =>
        Options.Create(new DisplayOptions
        {
            WidestSupportedViewportInPixels = widestInPixels,
            NarrowestSupportedViewportInPixels = narrowestInPixels,
        });

    static StarField Field(float widestInPixels, float narrowestInPixels = Narrowest) =>
        new(new Camera2D(), Declaring(widestInPixels, narrowestInPixels));

    static StarField Sown(float widestInPixels, float narrowestInPixels, params StarLayer[] layers) =>
        new(new Camera2D(), Declaring(widestInPixels, narrowestInPixels)) { Layers = layers };

    static ArgumentException Sowing(float widestInPixels, params StarLayer[] layers) =>
        Assert.Throws<ArgumentException>(() => _ = Sown(widestInPixels, Narrowest, layers));

    static bool NamesTheDeclaration(ArgumentException error) =>
        error.Message.Contains(
            nameof(DisplayOptions.WidestSupportedViewportInPixels),
            StringComparison.Ordinal);

    [Fact]
    public void AtTheExactCrossingPoint_TheStarIsStillBlamed_AndThatAdviceCanBeTaken()
    {
        // The knife edge, and the case that would let this change reintroduce the very defect it
        // fixes. Here the floor under a star of no size *equals* the ceiling, so the declaration
        // branch does not turn over and the star is named. That is only the right answer if
        // lowering SizeInPixels genuinely works — if no star of any size fitted here, "Lower
        // SizeInPixels" would be advice into a wall, which is exactly what this change exists to
        // stop. So the second half proves the advice can be taken rather than assuming it.
        StarField field = Field(WidestThatExactlyMeetsTheCeiling);

        Assert.Equal(field.LargestUsableTileSize, field.SmallestUsableTileSize(0f));

        ArgumentException error = Sowing(
            WidestThatExactlyMeetsTheCeiling,
            new StarLayer(1f, 360f, 1, 3f));

        Assert.False(
            NamesTheDeclaration(error),
            $"the declaration still admits a layer here, so the star is the thing to lower: {error.Message}");
        Assert.Contains("Lower SizeInPixels", error.Message, StringComparison.Ordinal);

        // and the advice, taken: a small enough star clears the floor and is accepted
        StarField sown = Sown(
            WidestThatExactlyMeetsTheCeiling,
            Narrowest,
            new StarLayer(1f, 360f, 1, 0.001f));

        Assert.Single(sown.Layers);
    }

    [Fact]
    public void OneStepPastTheCrossingPoint_TheDeclarationIsNamedInstead()
    {
        // the other side of the same line: one pixel wider and no star of any size fits, so the
        // branch turns over. A check written with the comparison the wrong way round, or against
        // the floor under the star in hand rather than under a star of no size, lands here
        ArgumentException error = Sowing(
            WidestThatExactlyMeetsTheCeiling + 1f,
            new StarLayer(1f, 360f, 1, 3f));

        Assert.True(
            NamesTheDeclaration(error),
            $"no star fits between these screens, so the declaration is the thing to move: {error.Message}");
        Assert.Contains(
            nameof(DisplayOptions.NarrowestSupportedViewportInPixels),
            error.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Lower SizeInPixels", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0001f)]
    [InlineData(3f)]
    [InlineData(10_000f)]
    public void WhenTheDeclarationAdmitsNothing_TheLayerIsABystander_WhateverStarItCarries(float sizeInPixels)
    {
        // the claim the message makes — "no SizeInPixels would help" — read back as a test. If the
        // branch depended on the star at all, one of these would come back blaming it, and a star
        // ten thousand pixels across is the one that would slip through a check that compared the
        // wrong floor
        ArgumentException error = Sowing(100_000f, new StarLayer(1f, 200f, 1, sizeInPixels));

        Assert.True(
            NamesTheDeclaration(error),
            $"a {sizeInPixels} pixel star is a bystander to a declaration nothing fits: {error.Message}");
    }

    [Fact]
    public void WhenTheDeclarationAdmitsNothing_ItIsNamedOnTheFirstLayer_NotTheOneThatHappensToBeWorst()
    {
        // several layers, the first of them perfectly ordinary. The reader should be told about the
        // declaration rather than led through the layers one at a time
        ArgumentException error = Sowing(
            100_000f,
            new StarLayer(1f, 200f, 1, 3f),
            new StarLayer(0.5f, 300f, 1, 50_000f));

        Assert.True(NamesTheDeclaration(error), error.Message);
    }

    [Fact]
    public void AFixedResolutionBuild_CannotCrossTheBoundsOnItsOwn()
    {
        // narrowest equal to widest is explicitly allowed, and the two bounds are 126 apart at
        // that point, so the pair can never admit nothing. Pinned because the ratio is the only
        // thing standing between a legal declaration and an error nobody could act on
        StarField field = Field(7680f, narrowestInPixels: 7680f);

        Assert.True(field.SmallestUsableTileSize(0f) < field.LargestUsableTileSize);

        StarField sown = Sown(7680f, 7680f, new StarLayer(1f, 200f, 1, 3f));

        Assert.Single(sown.Layers);
    }
}
