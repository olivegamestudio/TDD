using System.Numerics;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the star field through a recording renderer. Almost everything that matters about a
/// star field is a statement about where sprites ended up and how they moved, and that is a list
/// of sprites: only how it actually looks needs an eye.
/// </summary>
/// <remarks>
/// The layers these tests sow are deliberately finer than the ones the game ships. A tile at most
/// half the size of the region being asserted on means the region contains at least one whole
/// tile, so "there is a star here" holds by construction rather than by the field happening to
/// have sown one there.
/// </remarks>
public sealed class StarFieldTests
{
    /// <summary>
    /// How far from the edge of the viewport a star has to be for a test to hold it to still
    /// being there after the camera has moved. Comfortably more than any shift applied below, so
    /// a star that honestly left the screen is not mistaken for one that vanished.
    /// </summary>
    const float SafeMargin = 120f;

    /// <summary>
    /// Positions are floats through a float transform, so they are compared to within a fraction
    /// of a pixel rather than exactly.
    /// </summary>
    const float Tolerance = 0.05f;

    /// <summary>
    /// The screen the game declares it supports. Passed explicitly rather than defaulted, because
    /// every floor these tests assert on is measured against it and a test that did not say which
    /// screen it meant would be asserting on a number from somewhere else.
    /// </summary>
    /// <param name="widestInPixels">
    /// The declared width to measure against. Left null, the width the game actually ships with.
    /// </param>
    static IOptions<DisplayOptions> Declaring(float? widestInPixels = null) =>
        Options.Create(widestInPixels is null
            ? new DisplayOptions()
            : new DisplayOptions { WidestSupportedViewportInPixels = widestInPixels.Value });

    static StarField FieldOf(ICamera camera, params StarLayer[] layers) =>
        new(camera, Declaring()) { Layers = layers };

    /// <summary>The field the game ships — the default layers, and the declared screen.</summary>
    static StarField ShippingField(ICamera camera) => new(camera, Declaring());

    static Sprite[] RenderTo(RecordingRenderer renderer, StarField field)
    {
        renderer.Clear();
        field.Render(renderer);
        return [.. renderer.Drawn];
    }

    static bool WellInside(Vector2 position, Vector2 viewport) =>
        position.X > SafeMargin
        && position.Y > SafeMargin
        && position.X < viewport.X - SafeMargin
        && position.Y < viewport.Y - SafeMargin;

    static void AssertSomewhereNear(IEnumerable<Sprite> drawn, Vector2 position) =>
        Assert.Contains(drawn, sprite => Vector2.Distance(sprite.Position, position) < Tolerance);

    /// <summary>
    /// Asserts that a grid of <paramref name="columns"/> by <paramref name="rows"/> cells laid
    /// over the viewport has a star in every one of them — "no gap, no seam" said in a way a
    /// recording renderer can answer.
    /// </summary>
    /// <param name="where">
    /// What was being drawn, so a failure names the case rather than only the cell.
    /// </param>
    static void AssertNoEmptyCell(RecordingRenderer renderer, int columns, int rows, string where)
    {
        Vector2 cell = new(renderer.ViewportSize.X / columns, renderer.ViewportSize.Y / rows);

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                Vector2 corner = new(column * cell.X, row * cell.Y);

                Assert.True(
                    renderer.Drawn.Any(sprite =>
                        sprite.Position.X >= corner.X
                        && sprite.Position.X < corner.X + cell.X
                        && sprite.Position.Y >= corner.Y
                        && sprite.Position.Y < corner.Y + cell.Y),
                    $"No star in the cell at column {column}, row {row}, {where}.");
            }
        }
    }

    /// <summary>
    /// Asserts that every star well inside the viewport in <paramref name="before"/> is still
    /// there in <paramref name="after"/>, moved by <paramref name="shift"/> and nothing else.
    /// </summary>
    /// <param name="ofScale">
    /// Which layer to look at, identified by the size it draws its stars at. Left null, every
    /// star drawn is considered.
    /// </param>
    static void AssertMovedBy(
        IReadOnlyList<Sprite> before,
        IReadOnlyList<Sprite> after,
        Vector2 shift,
        Vector2 viewport,
        float? ofScale = null)
    {
        Vector2[] expected =
        [
            .. before
                .Where(sprite => ofScale is null || sprite.Scale == ofScale)
                .Where(sprite => WellInside(sprite.Position, viewport))
                .Select(sprite => sprite.Position + shift)
        ];

        // Without this the loop below would pass by being empty, which is no test at all.
        Assert.NotEmpty(expected);

        Sprite[] candidates = [.. after.Where(sprite => ofScale is null || sprite.Scale == ofScale)];

        foreach (Vector2 position in expected)
        {
            AssertSomewhereNear(candidates, position);
        }
    }

    [Fact]
    public void Render_PutsStarsOnScreen()
    {
        // a field this sparse would not read as a star field, whatever else the tests below say
        const int Sparse = 50;

        RecordingRenderer renderer = new();

        ShippingField(new Camera2D()).Render(renderer);

        Assert.True(
            renderer.Drawn.Count > Sparse,
            $"Expected more than {Sparse} stars, but drew {renderer.Drawn.Count}.");
    }

    [Fact]
    public void Render_LoadsTheStarTexture_OnceForEveryLayerAndEveryFrame()
    {
        // one texture serves every star at every distance; loading it per layer or per frame is
        // work that does not need doing
        RecordingRenderer renderer = new();
        StarField field = ShippingField(new Camera2D());

        field.Render(renderer);
        field.Render(renderer);

        Assert.Equal(StarField.AssetKey, Assert.Single(renderer.Textures.Requested));
    }

    [Fact]
    public void Render_HoldsTheFieldStill_WhileTheCameraIs()
    {
        // criterion one: with the ship stationary, the stars are on screen and they are still
        RecordingRenderer renderer = new();
        StarField field = ShippingField(new Camera2D());

        Sprite[] first = RenderTo(renderer, field);
        Sprite[] second = RenderTo(renderer, field);

        Assert.Equal(
            first.Select(sprite => sprite.Position),
            second.Select(sprite => sprite.Position));
    }

    [Fact]
    public void Render_MovesTheStarsDownTheScreen_AsTheCameraGoesForward()
    {
        // world forward is screen up, so flying forward sends everything fixed in the world down
        Camera2D camera = new();
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 3f));
        RecordingRenderer renderer = new();

        Sprite[] before = RenderTo(renderer, field);
        camera.Target = new Vector2(0f, 40f);
        Sprite[] after = RenderTo(renderer, field);

        AssertMovedBy(before, after, new Vector2(0f, 40f), renderer.ViewportSize);
    }

    [Fact]
    public void Render_MovesTheStarsLeft_AsTheCameraGoesToStarboard()
    {
        Camera2D camera = new();
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 3f));
        RecordingRenderer renderer = new();

        Sprite[] before = RenderTo(renderer, field);
        camera.Target = new Vector2(40f, 0f);
        Sprite[] after = RenderTo(renderer, field);

        AssertMovedBy(before, after, new Vector2(-40f, 0f), renderer.ViewportSize);
    }

    [Fact]
    public void Render_FollowsTheCamera_WhereverItIsPointed()
    {
        // nothing here knows a ship exists; it draws what is behind wherever the camera looks, so
        // it stays right the day something else is being followed
        Camera2D camera = new() { Target = new Vector2(0f, 900f) };
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 3f));
        RecordingRenderer renderer = new();

        Sprite[] before = RenderTo(renderer, field);
        camera.Target = new Vector2(0f, 930f);
        Sprite[] after = RenderTo(renderer, field);

        AssertMovedBy(before, after, new Vector2(0f, 30f), renderer.ViewportSize);
    }

    [Fact]
    public void Render_SpreadsTheStarsOutFromTheMiddle_WhenTheZoomRises()
    {
        // the camera's own convention, and the reason the field goes through it rather than
        // working out its own transform: a zoom spreads the world out from the middle of the
        // viewport, and the stars go with it
        Camera2D camera = new();
        StarField field = FieldOf(camera, new StarLayer(1f, 40f, 2, 3f));
        RecordingRenderer renderer = new();

        Sprite[] before = RenderTo(renderer, field);
        camera.PixelsPerUnit = 2f;
        Sprite[] after = RenderTo(renderer, field);

        Vector2 centre = renderer.ViewportCentre;

        Vector2[] expected =
        [
            .. before
                .Select(sprite => centre + ((sprite.Position - centre) * 2f))
                .Where(position => WellInside(position, renderer.ViewportSize))
        ];

        Assert.NotEmpty(expected);

        foreach (Vector2 position in expected)
        {
            AssertSomewhereNear(after, position);
        }
    }

    [Fact]
    public void Render_MovesNearerLayersFurtherThanDistantOnes()
    {
        // criterion three, and the whole reason there is more than one layer: without it the
        // field reads as a texture sliding past rather than as depth
        Camera2D camera = new();
        StarField field = FieldOf(
            camera,
            new StarLayer(0.25f, 100f, 2, 2f),
            new StarLayer(1.00f, 100f, 2, 8f));
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        Sprite[] before = RenderTo(renderer, field);
        camera.Target = new Vector2(0f, 40f);
        Sprite[] after = RenderTo(renderer, field);

        // the layers are told apart by the size they draw at: two pixels of a sixteen-pixel
        // sprite for the distant one, eight for the near one
        AssertMovedBy(before, after, new Vector2(0f, 10f), renderer.ViewportSize, ofScale: 2f / 16f);
        AssertMovedBy(before, after, new Vector2(0f, 40f), renderer.ViewportSize, ofScale: 8f / 16f);
    }

    [Fact]
    public void Render_NeitherLosesNorGainsAStar_InsideTheViewport()
    {
        // criterion four's sharpest edge: a star that winks out while the player is looking
        // straight at it is worse than no star field at all
        Camera2D camera = new() { Target = new Vector2(-3_450f, 12_800f) };
        StarLayer[] layers =
        [
            new StarLayer(0.30f, 120f, 2, 2f),
            new StarLayer(0.60f, 120f, 2, 4f),
            new StarLayer(1.00f, 120f, 2, 8f),
        ];
        StarField field = FieldOf(camera, layers);
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        Sprite[] before = RenderTo(renderer, field);
        camera.Target += new Vector2(17f, -23f);
        Sprite[] after = RenderTo(renderer, field);

        foreach (StarLayer layer in layers)
        {
            Vector2 shift = new(-17f * layer.Parallax, -23f * layer.Parallax);
            float scale = layer.SizeInPixels / 16f;

            // nothing vanished...
            AssertMovedBy(before, after, shift, renderer.ViewportSize, scale);

            // ...and nothing appeared out of nowhere either
            AssertMovedBy(after, before, -shift, renderer.ViewportSize, scale);
        }
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(37f, -91f)]
    [InlineData(4_000f, 12_000f)]
    [InlineData(-96_000f, 250_000f)]
    [InlineData(1_000_000f, -1_000_000f)]
    public void Render_CoversTheWholeViewport_HoweverFarTheCameraHasTravelled(float x, float y)
    {
        // criterion four: no gap and no seam, wherever the player has flown to
        const int Columns = 4;
        const int Rows = 3;

        Camera2D camera = new() { Target = new Vector2(x, y) };
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 1, 3f));
        RecordingRenderer renderer = new();

        field.Render(renderer);

        AssertNoEmptyCell(renderer, Columns, Rows, $"camera at ({x}, {y})");
    }

    [Fact]
    public void Render_CostsTheSame_HoweverFarTheCameraHasTravelled()
    {
        // criterion six. A field held as a list of positions would fail this by growing; a field
        // derived from the tiles the viewport covers cannot.
        Camera2D camera = new();
        StarField field = ShippingField(camera);
        RecordingRenderer renderer = new();

        int atTheStart = RenderTo(renderer, field).Length;
        camera.Target = new Vector2(0f, 5_000_000f);
        int aLongWayOut = RenderTo(renderer, field).Length;

        // not identical: which tiles the viewport straddles shifts by one as it moves. Bounded is
        // the claim, and bounded is what is asserted.
        Assert.InRange(aLongWayOut, atTheStart / 2, atTheStart * 2);
    }

    [Fact]
    public void Render_DrawsEveryStarAtItsLayersSize_AndNoneOfThemTurned()
    {
        Camera2D camera = new();
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 4f));
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        field.Render(renderer);

        Assert.NotEmpty(renderer.Drawn);
        Assert.All(renderer.Drawn, sprite =>
        {
            Assert.Equal(4f / 16f, sprite.Scale);
            Assert.Equal(0f, sprite.Rotation);
            Assert.Equal(new Vector2(8f, 8f), sprite.Origin);
        });
    }

    [Fact]
    public void Render_KeepsAStarTheSameSizeInPixels_WhateverTheZoom()
    {
        // a star is a point of light at an unreachable distance; zooming in on the world should
        // not turn it into a disc
        Camera2D camera = new() { PixelsPerUnit = 8f };
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 4f));
        RecordingRenderer renderer = new();
        renderer.Textures.SetSize(StarField.AssetKey, 16, 16);

        field.Render(renderer);

        Assert.NotEmpty(renderer.Drawn);
        Assert.All(renderer.Drawn, sprite => Assert.Equal(4f / 16f, sprite.Scale));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Render_DrawsNothing_WhenTheZoomIsNotPositive(float pixelsPerUnit)
    {
        // nothing is on screen at a zoom of nothing, and working out which tiles to visit would
        // be a division that means nothing
        Camera2D camera = new() { PixelsPerUnit = pixelsPerUnit };
        RecordingRenderer renderer = new();

        ShippingField(camera).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Render_DrawsNothing_BeforeThereIsAViewportToDrawInto()
    {
        // a window can report nothing before it is shown; the field should wait rather than throw
        RecordingRenderer renderer = new() { ViewportSize = Vector2.Zero };

        ShippingField(new Camera2D()).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Render_BoundsTheWorkPerFrame_HoweverFarTheZoomIsWoundOut()
    {
        // without the cap this zoom is a loop measured in millions rather than a frame
        Camera2D camera = new() { PixelsPerUnit = 0.000_01f };
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 1, 3f));
        RecordingRenderer renderer = new();

        field.Render(renderer);

        // one star per tile, so this is exactly the cap squared
        Assert.InRange(renderer.Drawn.Count, 1, StarField.MaxTilesPerAxis * StarField.MaxTilesPerAxis);
    }

    [Fact]
    public void Layers_KeepsTheStarsSownWhereTheyWerePutIn()
    {
        StarLayer[] layers = [new StarLayer(0.5f, 200f, 3, 2f)];

        StarField field = FieldOf(new Camera2D(), layers);

        Assert.Equal(layers, field.Layers);
    }

    [Theory]
    // a layer that never moves is painted on the inside of the canopy
    [InlineData(0f, 200f, 1, 2f)]
    [InlineData(-0.5f, 200f, 1, 2f)]
    // and one that outruns the world is not behind the world
    [InlineData(1.5f, 200f, 1, 2f)]
    // the rest would fail as a division, an empty field, or an invisible star
    [InlineData(0.5f, 0f, 1, 2f)]
    [InlineData(0.5f, -200f, 1, 2f)]
    [InlineData(0.5f, 200f, 0, 2f)]
    [InlineData(0.5f, 200f, 1, 0f)]
    public void Layers_RefusesALayerThatCouldNotBeDrawn(
        float parallax,
        float tileSize,
        int starsPerTile,
        float sizeInPixels)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = FieldOf(new Camera2D(), new StarLayer(parallax, tileSize, starsPerTile, sizeInPixels));
        });
    }

    [Theory]
    // NaN is the one that gets through everything: every bound in the validation is an ordered
    // comparison, and an ordered comparison against NaN is false whichever way round it is
    // written, so each guard reads as though it covers this and none of them does.
    [InlineData(float.NaN, 200f, 1, 2f, nameof(StarLayer.Parallax))]
    [InlineData(0.5f, float.NaN, 1, 2f, nameof(StarLayer.TileSizeInWorldUnits))]
    [InlineData(0.5f, 200f, 1, float.NaN, nameof(StarLayer.SizeInPixels))]
    // an infinite tile size clears the floor rather than tripping it, and then sows its stars
    // where no camera will ever be
    [InlineData(0.5f, float.PositiveInfinity, 1, 2f, nameof(StarLayer.TileSizeInWorldUnits))]
    // this one was already refused, but by the floor check, which named the tile size as the
    // thing to fix when the star's size is what is wrong
    [InlineData(0.5f, 200f, 1, float.PositiveInfinity, nameof(StarLayer.SizeInPixels))]
    public void Layers_RefusesALayerWhoseNumbersAreNotFinite(
        float parallax,
        float tileSize,
        int starsPerTile,
        float sizeInPixels,
        string offendingField)
    {
        // a blank border was worth an issue; each of these draws a blank *field*, through the
        // documented way to re-sow it
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            _ = FieldOf(new Camera2D(), new StarLayer(parallax, tileSize, starsPerTile, sizeInPixels));
        });

        // naming the wrong field is barely better than not naming one
        Assert.Contains(offendingField, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DrawsNothing_WhenTheZoomIsNotFinite()
    {
        // the same shape as the layer guards: `PixelsPerUnit <= 0f` is an ordered comparison, so
        // a NaN zoom walks past it into arithmetic that means nothing
        Camera2D camera = new() { PixelsPerUnit = float.NaN };
        RecordingRenderer renderer = new();

        ShippingField(camera).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Render_DrawsNothing_WhenTheViewportIsNotFinite()
    {
        RecordingRenderer renderer = new() { ViewportSize = new Vector2(float.NaN, 600f) };

        ShippingField(new Camera2D()).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Layers_RefusesATileSizeTooSmallToFillTheScreen()
    {
        // QA's reproduction on #40, which passed validation before it: at one pixel per unit this
        // layer draws 65,536 stars into a band in the middle of an 800x600 viewport and leaves a
        // blank border about 144 pixels wide the whole way round. Drawing more stars than any
        // shipping layer and still failing "no gap, no seam" is the failure worth catching early.
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            _ = FieldOf(new Camera2D(), new StarLayer(1f, 2f, 1, 3f));
        });

        // a message that does not name the number to change only says that something is wrong
        Assert.Contains(nameof(StarLayer.TileSizeInWorldUnits), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Layers_AcceptsTheSmallestTileSizeThatCanFillTheScreen()
    {
        // the bound has to be reachable, or it is a ban on small tiles rather than a bound
        StarField shipping = ShippingField(new Camera2D());
        float smallest = shipping.SmallestUsableTileSize(3f);

        StarField field = FieldOf(new Camera2D(), new StarLayer(1f, smallest, 1, 3f));

        Assert.Equal(smallest, Assert.Single(field.Layers).TileSizeInWorldUnits);
    }

    [Fact]
    public void Render_CoversTheWidestSupportedViewport_AtTheSmallestUsableTileSize()
    {
        // and what makes the bound honest rather than a number: a layer sown right at it still
        // fills the widest screen the game says it supports, with the cap in force.
        //
        // Sixteen cells across, not four. #50 is here because the field was floored against a
        // screen the game had never agreed to, and a 4x4 grid did not notice: the band the cap
        // clips the field to still reached into the outer cells of a coarse grid, so the same
        // assertion passed at 7680 while a 16x16 grid found the corner empty. A grid whose cells
        // are wider than the border being looked for cannot see the border.
        const float StarSize = 3f;
        const int Cells = 16;

        StarField field = new StarField(new Camera2D(), Declaring())
        {
            Layers = [new StarLayer(1f, ShippingField(new Camera2D()).SmallestUsableTileSize(StarSize), 1, StarSize)],
        };

        RecordingRenderer renderer = new(
            field.WidestSupportedViewportInPixels,
            field.WidestSupportedViewportInPixels);

        field.Render(renderer);

        AssertNoEmptyCell(renderer, Cells, Cells, "at the smallest usable tile size");
    }

    [Theory]
    [InlineData(1920f)]
    [InlineData(3840f)]
    [InlineData(5120f)]
    [InlineData(7680f)]
    public void Render_CoversWhateverScreenIsDeclared_AtTheSmallestUsableTileSize(float widest)
    {
        // The floor is only as good as the screen it was taken against — that is the whole of #50.
        // Declare a different screen and the floor has to move with it, or the layer it accepts
        // bands on exactly the display the declaration promised. Measured at the four widths worth
        // caring about rather than only at the one the game ships with, so raising the declaration
        // is a change this test answers rather than a number nobody re-checks.
        const float StarSize = 3f;
        const int Cells = 16;

        StarField field = new StarField(new Camera2D(), Declaring(widest))
        {
            Layers = [new StarLayer(1f, new StarField(new Camera2D(), Declaring(widest))
                .SmallestUsableTileSize(StarSize), 1, StarSize)],
        };

        RecordingRenderer renderer = new(widest, widest);

        field.Render(renderer);

        AssertNoEmptyCell(renderer, Cells, Cells, $"at the floor for a declared {widest}-pixel screen");
    }

    [Theory]
    [InlineData(5120f)]
    [InlineData(7680f)]
    public void Render_BandsOnAScreenWiderThanTheOneItsFloorWasTakenAgainst(float actual)
    {
        // #50 reproduced rather than described. Sow a layer right at the floor for a 3840-pixel
        // screen — which is what the field used to be floored against, by a constant nothing in
        // the game had agreed to — and put it on a wider display. The corner comes up empty: the
        // floor is not a safety margin, it is a promise about a particular screen, and past that
        // screen the layer draws tens of thousands of stars into a band and leaves the border #40
        // was raised for.
        //
        // This is the test that says why the number had to move out of the star field and into
        // something the game states. Delete the declaration and this passes again by accident.
        const float StarSize = 3f;
        const int Cells = 16;

        float flooredAgainst4K = new StarField(new Camera2D(), Declaring(3840f))
            .SmallestUsableTileSize(StarSize);

        StarField field = new StarField(new Camera2D(), Declaring(3840f))
        {
            Layers = [new StarLayer(1f, flooredAgainst4K, 1, StarSize)],
        };

        RecordingRenderer renderer = new(actual, actual);
        field.Render(renderer);

        Assert.Throws<Xunit.Sdk.TrueException>(
            () => AssertNoEmptyCell(renderer, Cells, Cells, "floored against 3840"));
    }

    [Fact]
    public void SmallestUsableTileSize_RisesWithTheScreenTheGameDeclares()
    {
        // and the mechanism behind it, said directly: the floor is derived from the declaration,
        // not compiled in. A layer that clears the floor for a 4K screen is refused once the game
        // says it supports an 8K one — which is the failure landing where the number was changed,
        // rather than on a display nobody owns.
        const float StarSize = 3f;

        float atFourK = new StarField(new Camera2D(), Declaring(3840f)).SmallestUsableTileSize(StarSize);
        float atEightK = new StarField(new Camera2D(), Declaring(7680f)).SmallestUsableTileSize(StarSize);

        Assert.True(atEightK > atFourK, $"A wider screen must raise the floor, but {atEightK} <= {atFourK}.");

        Assert.Throws<ArgumentException>(() =>
        {
            _ = new StarField(new Camera2D(), Declaring(7680f))
            {
                Layers = [new StarLayer(1f, atFourK, 1, StarSize)],
            };
        });
    }

    [Fact]
    public void WidestSupportedViewportInPixels_IsWhateverTheGameDeclares()
    {
        // the one place the number lives is DisplayOptions; this is the field reading it rather
        // than holding a second copy that can drift out of agreement with it
        DisplayOptions display = new() { WidestSupportedViewportInPixels = 5120f };

        StarField field = new(new Camera2D(), Options.Create(display));

        Assert.Equal(5120f, field.WidestSupportedViewportInPixels);
        Assert.Equal(7680f, new DisplayOptions().WidestSupportedViewportInPixels);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1920f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_RefusesAScreenNothingCanBeMeasuredAgainst(float widest)
    {
        // Every bound on a layer is measured against this, so a declaration of zero or of NaN does
        // not fail here — it makes the floor meaningless and lets any layer at all through, which
        // is #40's shape again one level up. NaN in particular would pass the floor check silently,
        // because an ordered comparison against it is false whichever way round it is written.
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            _ = new StarField(new Camera2D(), Declaring(widest));
        });

        Assert.Contains(
            nameof(DisplayOptions.WidestSupportedViewportInPixels),
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-96_000f, 250_000f)]
    public void Render_CoversTheWholeViewport_WithTheLayersTheGameShips(float x, float y)
    {
        // #40's third criterion: whatever the validation and the doc now say, what the player
        // actually gets is unchanged — the shipping field still covers the viewport, at the
        // origin and a long way from it
        const int Cells = 3;

        StarField field = ShippingField(new Camera2D { Target = new Vector2(x, y) });
        RecordingRenderer renderer = new();

        field.Render(renderer);

        AssertNoEmptyCell(renderer, Cells, Cells, $"shipping layers, camera at ({x}, {y})");
    }

    [Fact]
    public void Render_ClipsTheFieldToABandAroundTheCamera_WhenTheZoomOutrunsTheCap()
    {
        // what the cap costs, pinned as a test rather than left to prose. No bound on tile size
        // can prevent this — a low enough zoom spans MaxTilesPerAxis tiles of any size — so
        // MaxTilesPerAxis is documented as clipping rather than described as unreachable.
        const float Border = 100f;

        Camera2D camera = new() { PixelsPerUnit = 0.001f };
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 1, 3f));
        RecordingRenderer renderer = new();

        field.Render(renderer);

        Assert.NotEmpty(renderer.Drawn);
        Assert.All(renderer.Drawn, sprite =>
        {
            Assert.InRange(sprite.Position.X, Border, renderer.ViewportSize.X - Border);
            Assert.InRange(sprite.Position.Y, Border, renderer.ViewportSize.Y - Border);
        });
    }

    [Fact]
    public void Render_StillDrawsStarsAtFullSize_WhenTheZoomOutrunsTheCap()
    {
        // The band above was justified by a star being a fraction of a pixel at a zoom that low,
        // so that clipping the field costs the player nothing they could see. It does not work
        // out that way: StarLayer.SizeInPixels is in pixels by design — "zooming in on the world
        // should not make it a disc" — so the scale a star is drawn at does not depend on the
        // zoom at all. The blank border sits beside stars at their full size, which is the same
        // "passes and still looks broken" shape #40 was raised for.
        const float StarSize = 3f;
        StarLayer layer = new(1f, 100f, 1, StarSize);
        RecordingRenderer renderer = new();

        Sprite[] atFullZoom = RenderTo(renderer, FieldOf(new Camera2D(), layer));
        Sprite[] pastTheCap = RenderTo(
            renderer,
            FieldOf(new Camera2D { PixelsPerUnit = 0.001f }, layer));

        Assert.NotEmpty(pastTheCap);
        Assert.Equal(atFullZoom[0].Scale, pastTheCap[0].Scale);
        Assert.All(pastTheCap, sprite => Assert.Equal(atFullZoom[0].Scale, sprite.Scale));
    }

    [Fact]
    public void DefaultLayers_CanEachFillTheScreen()
    {
        // the shipping layers are nowhere near the cap, and this is what says so out loud
        StarField field = ShippingField(new Camera2D());

        Assert.All(field.Layers, layer => Assert.True(
            layer.TileSizeInWorldUnits >= field.SmallestUsableTileSize(layer.SizeInPixels),
            $"A tile size of {layer.TileSizeInWorldUnits} cannot fill the screen."));
    }

    [Fact]
    public void DefaultLayers_StateHowMuchWiderAScreenCouldBeDeclared()
    {
        // #50's third criterion, and the number DisplayOptions quotes back at anyone raising the
        // declaration. The shipping layers are not re-validated against a configured screen — they
        // are the game's content, not a caller's — so what stops a wider declaration quietly
        // banding the shipping field is knowing where its reach runs out. The finest layer is 90
        // world units, and MaxTilesPerAxis - 4 of those is a little over 22,000 pixels.
        const float Stated = 22_000f;

        StarField field = ShippingField(new Camera2D());

        float reach = field.Layers.Min(layer =>
            (layer.TileSizeInWorldUnits * (StarField.MaxTilesPerAxis - 4f)) - (2f * layer.SizeInPixels));

        Assert.True(
            reach >= Stated,
            $"DisplayOptions says the shipping layers cover a little over {Stated} pixels, "
            + $"but they reach {reach}.");

        // and the declaration the game actually ships with is comfortably inside that
        Assert.True(
            field.WidestSupportedViewportInPixels < reach,
            $"The declared {field.WidestSupportedViewportInPixels}-pixel screen is wider than the "
            + $"{reach} pixels the shipping layers cover.");
    }

    [Fact]
    public void DefaultLayers_AreSownAtDifferentDepths()
    {
        // one layer moving is a texture sliding past; the depth is in the difference between them
        StarField field = ShippingField(new Camera2D());

        Assert.True(field.Layers.Count > 1);
        Assert.Equal(field.Layers.Count, field.Layers.Select(layer => layer.Parallax).Distinct().Count());
        Assert.All(field.Layers, layer => Assert.InRange(layer.Parallax, 0.000_1f, 1f));
    }
}
