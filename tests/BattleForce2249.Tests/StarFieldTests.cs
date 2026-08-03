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

    /// <summary>
    /// Asserts that at least one star was drawn inside the box of <paramref name="size"/> pixels
    /// whose top left corner is <paramref name="corner"/>.
    /// </summary>
    /// <remarks>
    /// The layers these tests sow put a star in every tile, so a box two tiles across contains
    /// one by construction — it holds a whole tile wherever the grid happens to fall.
    /// </remarks>
    static void AssertAStarIn(IEnumerable<Sprite> drawn, Vector2 corner, Vector2 size) =>
        Assert.Contains(
            drawn,
            sprite => sprite.Position.X >= corner.X
                && sprite.Position.Y >= corner.Y
                && sprite.Position.X <= corner.X + size.X
                && sprite.Position.Y <= corner.Y + size.Y);

    /// <summary>
    /// Asserts that every star near the middle of the viewport in <paramref name="before"/> is
    /// still there in <paramref name="after"/>, swung about <paramref name="centre"/> by
    /// <paramref name="orientation"/> and by nothing else.
    /// </summary>
    /// <remarks>
    /// Only the stars near the middle are held to it. A star out towards the edge is genuinely
    /// carried off screen by a large turn, and this is about the ones that stay.
    /// </remarks>
    static void AssertTurnedBy(
        IReadOnlyList<Sprite> before,
        IReadOnlyList<Sprite> after,
        float orientation,
        Vector2 centre)
    {
        // Comfortably inside the shorter half of the viewport, so a star this close to the middle
        // is still on screen at any angle and cannot be mistaken for one that vanished.
        const float StaysOnScreen = 250f;

        // The picture turns the opposite way to the camera: turn to starboard and the world
        // swings to port. On screen, where Y points down, that is a rotation by the negative of
        // the orientation.
        float sin = MathF.Sin(-orientation);
        float cos = MathF.Cos(-orientation);

        Vector2[] expected =
        [
            .. before
                .Select(sprite => sprite.Position - centre)
                .Where(offset => offset.Length() < StaysOnScreen)
                .Select(offset => centre + new Vector2(
                    (offset.X * cos) - (offset.Y * sin),
                    (offset.X * sin) + (offset.Y * cos)))
        ];

        // Without this the loop below would pass by being empty, which is no test at all.
        Assert.NotEmpty(expected);

        foreach (Vector2 position in expected)
        {
            AssertSomewhereNear(after, position);
        }
    }

    [Theory]
    [InlineData(MathF.PI / 4f)]
    [InlineData(-MathF.PI / 4f)]
    [InlineData(3f * MathF.PI / 4f)]
    [InlineData(0.6f)]
    public void Render_FillsTheCornersOfTheScreen_WhileTheCameraIsTurned(float orientation)
    {
        // the viewport is upright on the screen and turned in the world, so its corners reach
        // further along the world's own axes than its edges do. Sow only the upright box and
        // those corners are empty: the player watches the stars drain out of the corners of the
        // window as the ship turns, worst at the diagonals, and the field stops reading as a sky.
        // A fine layer and a small box, deliberately: a box wide enough to reach back towards the
        // middle of the screen is satisfied by a star nowhere near the corner, which is exactly
        // the star an unturned field still sows. The box is five tiles across, so it holds a whole
        // tile at any angle and a star in it is guaranteed rather than lucky.
        Camera2D camera = new() { Orientation = orientation };
        StarField field = FieldOf(camera, new StarLayer(1f, 20f, 1, 3f));
        RecordingRenderer renderer = new();

        Sprite[] drawn = RenderTo(renderer, field);

        Vector2 box = new(100f, 100f);
        AssertAStarIn(drawn, Vector2.Zero, box);
        AssertAStarIn(drawn, new Vector2(renderer.ViewportSize.X - box.X, 0f), box);
        AssertAStarIn(drawn, new Vector2(0f, renderer.ViewportSize.Y - box.Y), box);
        AssertAStarIn(drawn, renderer.ViewportSize - box, box);
    }

    [Theory]
    [InlineData(MathF.PI / 2f)]
    [InlineData(-MathF.PI / 2f)]
    [InlineData(0.8f)]
    public void Render_SwingsTheFieldAboutTheMiddle_AsTheCameraTurns(float orientation)
    {
        // the stars go through the same transform the ship does, so turning the camera turns the
        // field about the point it is holding and moves nothing relative to anything else. This
        // is what a rotating camera has to look like: the ship still, the sky swinging past it.
        Camera2D camera = new();
        StarField field = FieldOf(camera, new StarLayer(1f, 100f, 2, 3f));
        RecordingRenderer renderer = new();

        Sprite[] upright = RenderTo(renderer, field);
        camera.Orientation = orientation;
        Sprite[] turned = RenderTo(renderer, field);

        AssertTurnedBy(upright, turned, orientation, renderer.ViewportCentre);
    }

    [Fact]
    public void Render_KeepsTheLayersInStep_WhileTheCameraIsTurned()
    {
        // the parallax offset is applied in the world and then turned with everything else, so a
        // near layer and a far one swing together. Applied after the turn instead, a layer would
        // slide off in a direction of its own the moment the ship stopped pointing north.
        Camera2D camera = new()
        {
            Target = new Vector2(400f, 900f),
            Orientation = MathF.PI / 3f,
        };
        StarField field = FieldOf(
            camera,
            new StarLayer(0.4f, 100f, 2, 3f),
            new StarLayer(1f, 100f, 2, 6f));
        RecordingRenderer renderer = new();

        Sprite[] before = RenderTo(renderer, field);
        camera.Target += new Vector2(0f, 50f);
        Sprite[] after = RenderTo(renderer, field);

        // flying 50 units forward with the camera turned a third of a half-turn to starboard
        // sends the fixed layer that far the other way, which is down and to the right of screen
        Vector2 shift = new(
            50f * MathF.Sin(MathF.PI / 3f),
            50f * MathF.Cos(MathF.PI / 3f));

        // the near layer travels the whole of it, the far layer its own share — in the same
        // direction, which is the part the turn could have got wrong
        AssertMovedBy(before, after, shift, renderer.ViewportSize, ofScale: 6f / 1024f);
        AssertMovedBy(before, after, shift * 0.4f, renderer.ViewportSize, ofScale: 3f / 1024f);
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

        Vector2 cell = new(renderer.ViewportSize.X / Columns, renderer.ViewportSize.Y / Rows);

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                Vector2 corner = new(column * cell.X, row * cell.Y);

                Assert.True(
                    renderer.Drawn.Any(sprite =>
                        sprite.Position.X >= corner.X
                        && sprite.Position.X < corner.X + cell.X
                        && sprite.Position.Y >= corner.Y
                        && sprite.Position.Y < corner.Y + cell.Y),
                    $"No star in the cell at column {column}, row {row}, camera at ({x}, {y}).");
            }
        }
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
        // be a division that means nothing
        UncheckedCamera camera = new() { PixelsPerUnit = pixelsPerUnit };
        RecordingRenderer renderer = new();

        new StarField(camera).Render(renderer);

        Assert.Empty(renderer.Drawn);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Render_DrawsNothing_WhenTheZoomIsNotFinite(float pixelsPerUnit)
    {
        // The camera refuses these where they are written, so this is about the guard here being
        // a true statement about any ICamera rather than about the one implementation that keeps
        // the rule. It used to be an ordered comparison against zero — and every ordered
        // comparison against NaN is false, so NaN was the one value that walked straight past the
        // check written to stop exactly this, and the field was drawn at positions that are
        // nowhere.
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
