using System.Numerics;
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

    static StarField FieldOf(ICamera camera, params StarLayer[] layers) =>
        new(camera) { Layers = layers };

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
    /// Asserts that at least one star was drawn within the viewport — the weakest thing that can
    /// be said of a field, and the one a layer sown too thinly fails.
    /// </summary>
    /// <param name="where">What was being drawn, so a failure names the case.</param>
    static void AssertSomethingOnScreen(RecordingRenderer renderer, string where) =>
        Assert.True(
            renderer.Drawn.Any(sprite =>
                sprite.Position.X >= 0f
                && sprite.Position.Y >= 0f
                && sprite.Position.X <= renderer.ViewportSize.X
                && sprite.Position.Y <= renderer.ViewportSize.Y),
            $"Not one of the {renderer.Drawn.Count} stars drawn is on the screen, {where}.");

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

        new StarField(new Camera2D()).Render(renderer);

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
        StarField field = new(new Camera2D());

        field.Render(renderer);
        field.Render(renderer);

        Assert.Equal(StarField.AssetKey, Assert.Single(renderer.Textures.Requested));
    }

    [Fact]
    public void Render_HoldsTheFieldStill_WhileTheCameraIs()
    {
        // criterion one: with the ship stationary, the stars are on screen and they are still
        RecordingRenderer renderer = new();
        StarField field = new(new Camera2D());

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
        StarField field = new(camera);
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
        // be a division that means nothing. Through an UncheckedCamera because #57 put the same
        // rule on Camera2D's setter: what is being covered here is the field's own guard against
        // an ICamera that did not keep the contract, which is still the field's to hold.
        UncheckedCamera camera = new() { PixelsPerUnit = pixelsPerUnit };
        RecordingRenderer renderer = new();

        new StarField(camera).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Render_DrawsNothing_BeforeThereIsAViewportToDrawInto()
    {
        // a window can report nothing before it is shown; the field should wait rather than throw
        RecordingRenderer renderer = new() { ViewportSize = Vector2.Zero };

        new StarField(new Camera2D()).Render(renderer);

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
    // and the signed infinities on either side, which no ordered comparison distinguishes from a
    // number it would accept
    [InlineData(0.5f, float.NegativeInfinity, 1, 2f, nameof(StarLayer.TileSizeInWorldUnits))]
    [InlineData(float.PositiveInfinity, 200f, 1, 2f, nameof(StarLayer.Parallax))]
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
        // a NaN zoom walks past it into arithmetic that means nothing. Camera2D now refuses this
        // where it is set (#57), so it takes an UncheckedCamera to still get one in here.
        UncheckedCamera camera = new() { PixelsPerUnit = float.NaN };
        RecordingRenderer renderer = new();

        new StarField(camera).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Fact]
    public void Render_DrawsNothing_WhenTheViewportIsNotFinite()
    {
        RecordingRenderer renderer = new() { ViewportSize = new Vector2(float.NaN, 600f) };

        new StarField(new Camera2D()).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Theory]
    [InlineData(float.NaN, float.NaN)]
    [InlineData(0f, float.NaN)]
    [InlineData(float.PositiveInfinity, 0f)]
    public void Render_CannotBeScatteredAtNaN_ByACameraThatFollowedANonFinitePosition(
        float x,
        float y)
    {
        // #57, end to end: the field's own guard covers the zoom and the viewport but cannot see
        // the camera's target, and a non-finite one used to draw the whole field at a position
        // that is nowhere — sixteen sprites, none of them on screen. The camera refuses it now,
        // so the frame that produced the number fails instead of the picture quietly emptying.
        Camera2D camera = new() { Target = new Vector2(613f, -227f) };
        StarField field = new(camera);
        RecordingRenderer renderer = new();

        Assert.Throws<ArgumentException>(() => camera.Target = new Vector2(x, y));

        field.Render(renderer);

        Assert.NotEmpty(renderer.Drawn);
        Assert.All(renderer.Drawn, sprite =>
            Assert.True(
                float.IsFinite(sprite.Position.X) && float.IsFinite(sprite.Position.Y),
                $"a star was drawn at {sprite.Position}"));
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
    public void Layers_RefusesATileSizeTooBigToPutAStarOnScreen()
    {
        // the far end of the same failure the floor closes: this layer passes every other guard,
        // draws a handful of stars, and can put none of them on screen, because a tile wider than
        // the viewport need not have a star anywhere the player is looking
        float tooBig = StarField.LargestUsableTileSize * 1.000_01f;

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            _ = FieldOf(new Camera2D(), new StarLayer(1f, tooBig, 1, 3f));
        });

        Assert.Contains(nameof(StarLayer.TileSizeInWorldUnits), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Layers_AcceptsTheLargestTileSizeThatCanPutAStarOnScreen()
    {
        // the ceiling has to be reachable, for the same reason the floor does — otherwise it is a
        // ban on sparse layers rather than a bound
        StarField field = FieldOf(
            new Camera2D(),
            new StarLayer(1f, StarField.LargestUsableTileSize, 1, 3f));

        Assert.Equal(StarField.LargestUsableTileSize, Assert.Single(field.Layers).TileSizeInWorldUnits);
    }

    [Fact]
    public void Layers_RefusesAStarSoBigThatNoTileSizeWouldWork_NamingTheStar()
    {
        // where the two bounds cross, the tile size is not the thing that is wrong: a star this
        // wide pushes the floor above the ceiling, so every tile size is refused and advice to
        // change the tile size sends the reader into a wall. Naming the wrong field is the defect
        // this validation was sent back for.
        const float Absurd = 50_000f;

        Assert.True(
            StarField.SmallestUsableTileSize(Absurd) > StarField.LargestUsableTileSize,
            "This star is not big enough to cross the bounds, so the test is not testing anything.");

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
        {
            _ = FieldOf(new Camera2D(), new StarLayer(1f, 200f, 1, Absurd));
        });

        Assert.Contains(nameof(StarLayer.SizeInPixels), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(179.5f, -359.5f)]
    [InlineData(359.9f, 0.1f)]
    [InlineData(-96_000f, 250_000f)]
    [InlineData(1_000_000f, -1_000_000f)]
    public void Render_PutsAStarOnTheNarrowestSupportedViewport_AtTheLargestUsableTileSize(
        float x,
        float y)
    {
        // what makes the ceiling honest rather than a number. Half the narrowest screen means the
        // screen always spans a whole tile, so that tile's star is on screen wherever the camera
        // stands — asserted at a camera deliberately parked between two tiles, and a long way out.
        StarField field = FieldOf(
            new Camera2D { Target = new Vector2(x, y) },
            new StarLayer(1f, StarField.LargestUsableTileSize, 1, 3f));

        RecordingRenderer renderer = new(
            StarField.NarrowestSupportedViewportInPixels,
            StarField.NarrowestSupportedViewportInPixels);

        field.Render(renderer);

        AssertSomethingOnScreen(renderer, $"camera at ({x}, {y})");
    }

    [Fact]
    public void Render_PutsAStarOnAWidescreenViewport_AtTheLargestUsableTileSize()
    {
        // the constant is the narrowest *side*, because a tile is square and has to fit the
        // shorter axis — 1280x720 is the screen it was named for
        StarField field = FieldOf(
            new Camera2D { Target = new Vector2(613f, -227f) },
            new StarLayer(1f, StarField.LargestUsableTileSize, 1, 3f));

        RecordingRenderer renderer = new(1_280f, StarField.NarrowestSupportedViewportInPixels);

        field.Render(renderer);

        AssertSomethingOnScreen(renderer, "on a 1280x720 screen");
    }

    [Fact]
    public void Layers_AcceptsTheSmallestTileSizeThatCanFillTheScreen()
    {
        // the bound has to be reachable, or it is a ban on small tiles rather than a bound
        float smallest = StarField.SmallestUsableTileSize(3f);

        StarField field = FieldOf(new Camera2D(), new StarLayer(1f, smallest, 1, 3f));

        Assert.Equal(smallest, Assert.Single(field.Layers).TileSizeInWorldUnits);
    }

    [Fact]
    public void Render_CoversTheWidestSupportedViewport_AtTheSmallestUsableTileSize()
    {
        // and what makes the bound honest rather than a number: a layer sown right at it still
        // fills the widest screen the game claims to support, with the cap in force
        const float StarSize = 3f;
        const int Cells = 4;

        StarField field = FieldOf(
            new Camera2D(),
            new StarLayer(1f, StarField.SmallestUsableTileSize(StarSize), 1, StarSize));

        RecordingRenderer renderer = new(
            StarField.WidestSupportedViewportInPixels,
            StarField.WidestSupportedViewportInPixels);

        field.Render(renderer);

        AssertNoEmptyCell(renderer, Cells, Cells, "at the smallest usable tile size");
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

        StarField field = new(new Camera2D { Target = new Vector2(x, y) });
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
        StarField field = new(new Camera2D());

        Assert.All(field.Layers, layer => Assert.True(
            layer.TileSizeInWorldUnits >= StarField.SmallestUsableTileSize(layer.SizeInPixels),
            $"A tile size of {layer.TileSizeInWorldUnits} cannot fill the screen."));

        // and the other end: a tile bigger than the screen fills nothing either
        Assert.All(field.Layers, layer => Assert.True(
            layer.TileSizeInWorldUnits <= StarField.LargestUsableTileSize,
            $"A tile size of {layer.TileSizeInWorldUnits} need not put a star on screen."));
    }

    [Fact]
    public void DefaultLayers_AreSownAtDifferentDepths()
    {
        // one layer moving is a texture sliding past; the depth is in the difference between them
        StarField field = new(new Camera2D());

        Assert.True(field.Layers.Count > 1);
        Assert.Equal(field.Layers.Count, field.Layers.Select(layer => layer.Parallax).Distinct().Count());
        Assert.All(field.Layers, layer => Assert.InRange(layer.Parallax, 0.000_1f, 1f));
    }
}
