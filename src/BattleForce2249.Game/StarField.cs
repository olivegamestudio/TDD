using System.Numerics;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The stars behind everything else: the fixed reference the player reads motion against.
/// </summary>
/// <remarks>
/// <para>
/// The camera holds the ship in the middle of the viewport, so the ship never moves on screen. On
/// a flat clear that leaves full thrust looking exactly like a standstill — the ship is visible,
/// but flight is not. This is what moves instead, and the parallax between its layers is what
/// gives the speed some depth.
/// </para>
/// <para>
/// <b>It follows the camera, not the ship.</b> Nothing here knows a ship exists; it draws what is
/// behind wherever the camera is pointed, so it stays correct the day something else is being
/// followed.
/// </para>
/// <para>
/// <b>The field is derived, not stored.</b> The world is unbounded and the player flies forward
/// indefinitely, so a list of star positions would run out. Each layer is instead sown on a grid
/// of square tiles of unbounded extent, and where a star stands is a function of the tile it
/// falls in — so only the tiles the viewport covers are ever visited, the field is continuous in
/// every direction, and the cost of a frame does not grow with how far the player has flown.
/// </para>
/// </remarks>
public sealed class StarField : IRenderable
{
    /// <summary>
    /// The asset key the stars are drawn from. An identifier, not text: it is never translated.
    /// </summary>
    /// <remarks>
    /// One small sprite serves every star at every distance, scaled down for the far layers. A
    /// star has no shape to get right, so a second asset would buy nothing.
    /// </remarks>
    public const string AssetKey = "star";

    /// <summary>
    /// How many tiles of one layer will be visited along an axis in a single frame, however far
    /// the camera is zoomed out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A backstop rather than a design constraint: without it, a wound-out zoom would put the
    /// frame into a loop measured in millions.
    /// </para>
    /// <para>
    /// <b>What reaching it costs.</b> The tile range is clamped around the tile the camera stands
    /// in, so past the cap the field stops at a band about the middle of the viewport and leaves
    /// a blank border the rest of the way out. That is the field failing to be a field, so it is
    /// worth being exact about when it happens.
    /// </para>
    /// <para>
    /// <b>When it is reached.</b> The cap bites when the viewport spans <c>MaxTilesPerAxis</c>
    /// tiles of a layer — when <c>viewportInPixels / (PixelsPerUnit × TileSizeInWorldUnits)</c>
    /// approaches this number. That is a joint condition on the viewport, the zoom
    /// <em>and</em> the layer, not on the zoom alone: a tile size small enough reaches it at an
    /// ordinary zoom, and a zoom low enough reaches it for a tile size of any size.
    /// </para>
    /// <para>
    /// <b>Only half of that is prevented.</b> <see cref="Layers"/> refuses a tile size too small
    /// to fill <see cref="WidestSupportedViewportInPixels"/> at one pixel per unit, so the half
    /// that is a mistake in the layer fails where the layer is written. The other half cannot be
    /// prevented by any bound on tile size, because a low enough <see cref="ICamera.PixelsPerUnit"/>
    /// spans this many tiles whatever their size.
    /// </para>
    /// <para>
    /// <b>And what is left is not free.</b> It would be convenient to say the band is honest at
    /// that zoom, on the grounds that a star would be a fraction of a pixel by then. It would not
    /// be: <see cref="StarLayer.SizeInPixels"/> is in pixels precisely so that zoom does not
    /// resize a star, so the blank border sits beside stars drawn at their full size. What bounds
    /// this half is the camera rather than the layer — nothing in the game moves
    /// <see cref="ICamera.PixelsPerUnit"/> off one — so it is a real limit on how far the field can
    /// be zoomed out, and it is written down as one rather than explained away. Explaining it away
    /// on exactly this reasoning is what #40 was raised for.
    /// </para>
    /// </remarks>
    public const int MaxTilesPerAxis = 256;

    /// <summary>
    /// The widest viewport a layer is required to fill: the reference <see cref="Layers"/> holds a
    /// tile size against, in pixels at one pixel per unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A layer knows neither the viewport nor the zoom, so a bound on its tile size can only be
    /// taken against a stated screen. That statement is not the star field's to make — it is what
    /// the game says it supports — so this is read from
    /// <see cref="DisplayOptions.WidestSupportedViewportInPixels"/> rather than decided here, and
    /// #50 is what happens when a rendering type invents the number instead: it was a constant of
    /// 3840 that nothing in the game agreed to, and every display wider than it banded.
    /// </para>
    /// <para>
    /// Held as its own value rather than read from the options each time, so the floor a layer was
    /// accepted against cannot change under a field that has already been sown.
    /// </para>
    /// </remarks>
    public float WidestSupportedViewportInPixels { get; }

    /// <summary>
    /// The smallest tile size a layer drawing stars <paramref name="sizeInPixels"/> across can be
    /// sown at and still cover <see cref="WidestSupportedViewportInPixels"/> within
    /// <see cref="MaxTilesPerAxis"/> tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The star's size comes into it because the tile range is widened by a star's width either
    /// side, so that one just off the edge is still drawn while part of it would show.
    /// <c>MaxTilesPerAxis - 4</c> rather than <c>- 1</c> leaves the slack the arithmetic needs: a
    /// tile at each end may be half-covered and counted anyway, and the clamp is a tile wider
    /// below the camera than above it, so a range that only just fits could still lose an edge
    /// depending on where between two tiles the camera happens to stand.
    /// </para>
    /// <para>
    /// An instance member rather than a static one because the screen it is measured against is
    /// configured, not compiled in — the floor is only meaningful alongside the display it was
    /// taken from.
    /// </para>
    /// </remarks>
    /// <param name="sizeInPixels">How big the layer draws each star, in pixels.</param>
    /// <returns>The smallest tile size, in world units, that fills the screen.</returns>
    public float SmallestUsableTileSize(float sizeInPixels) =>
        (WidestSupportedViewportInPixels + (2f * sizeInPixels)) / (MaxTilesPerAxis - 4f);

    /// <summary>
    /// The narrowest side of the smallest viewport a layer is required to put a star on, in pixels
    /// at one pixel per unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The companion to <see cref="WidestSupportedViewportInPixels"/>, and read the same way: from
    /// <see cref="DisplayOptions.NarrowestSupportedViewportInPixels"/> rather than decided here. A
    /// bound on a tile size can only be taken against a screen someone names, and naming it is the
    /// game's job rather than the star field's — which is the whole of #50, and applies at this end
    /// exactly as it does at the other.
    /// </para>
    /// <para>
    /// It is the narrowest <em>side</em> rather than a width because a tile is square and has to
    /// fit the shorter axis.
    /// </para>
    /// <para>
    /// Held as its own value rather than read from the options each time, for the same reason as
    /// <see cref="WidestSupportedViewportInPixels"/>: a ceiling that moved after a field was sown
    /// would leave layers in place that no longer clear it.
    /// </para>
    /// </remarks>
    public float NarrowestSupportedViewportInPixels { get; }

    /// <summary>
    /// The largest tile size a layer can be sown at and still be certain to put a star on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Half <see cref="NarrowestSupportedViewportInPixels"/>, because a span of two tile widths
    /// contains one whole tile whatever it is aligned against: a viewport at least two tiles across
    /// always has a tile entirely inside it, and that tile has at least one star, so at least one
    /// star is on screen wherever the camera stands.
    /// </para>
    /// <para>
    /// <b>This is the bound that can be proved, not the last one that works.</b> Measured against
    /// the hash as it stands, a layer only starts leaving the screen empty at around seven tenths
    /// of a screen per tile rather than a half, so tile sizes between the two are refused although
    /// they would in fact draw. That slack is deliberate: where a star lands inside its tile is a
    /// property of <see cref="Hash"/>, so the seven tenths moves the day the hash is changed and
    /// the half does not. A bound that holds only while the pseudo-randomness cooperates is the
    /// kind of "passes and still looks broken" this validation exists to prevent.
    /// </para>
    /// <para>
    /// <b>Why the far end needs a bound too.</b> A tile size too small draws tens of thousands of
    /// stars into a band and leaves a blank border; a tile size too large draws a handful and can
    /// leave the sky empty. Both are a layer accepted while being described as safe, which is what
    /// #40 was raised about, so both ends are closed rather than only the one noticed first.
    /// </para>
    /// <para>
    /// An instance member rather than a static one, because the screen it is halved from is
    /// configured rather than compiled in. A tile size is not too large on its own — it is too
    /// large for some declared display, and declaring a narrower one lowers this.
    /// </para>
    /// </remarks>
    public float LargestUsableTileSize => NarrowestSupportedViewportInPixels / 2f;

    static readonly StarLayer[] _defaultLayers =
    [
        // Far to near, which is also the order they are drawn in: the near layer stacks over the
        // far one. The nearest is fixed in the world, so it slides past at exactly the speed the
        // ship is flying, and the others lag by their share of it.
        new StarLayer(Parallax: 0.30f, TileSizeInWorldUnits: 90f, StarsPerTile: 2, SizeInPixels: 2.5f),
        new StarLayer(Parallax: 0.60f, TileSizeInWorldUnits: 150f, StarsPerTile: 2, SizeInPixels: 3.5f),
        new StarLayer(Parallax: 1.00f, TileSizeInWorldUnits: 230f, StarsPerTile: 2, SizeInPixels: 5f),
    ];

    readonly IReadOnlyList<StarLayer> _layers = _defaultLayers;

    readonly ICamera _camera;

    ITexture? _texture;

    /// <summary>
    /// Creates the field the camera looks out through.
    /// </summary>
    /// <remarks>
    /// The declared display is taken once, here, rather than read per frame: it decides which
    /// layers this field would accept, and a floor that moved after a field was sown would leave
    /// layers in place that no longer clear it.
    /// </remarks>
    /// <param name="camera">The camera the world is drawn through — the same one the ship uses.</param>
    /// <param name="display">What the game says about the screen it is drawn on.</param>
    /// <exception cref="ArgumentException">
    /// A declared viewport is not a finite positive number of pixels, or the declared narrowest
    /// screen is wider than the declared widest. Guarded here as well as where the options are
    /// bound, because a field can be built without going through the container, and every bound
    /// below is measured against these.
    /// </exception>
    public StarField(ICamera camera, IOptions<DisplayOptions> display)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(display);

        float widest = display.Value.WidestSupportedViewportInPixels;
        float narrowest = display.Value.NarrowestSupportedViewportInPixels;

        if (!IsDrawableExtent(widest))
        {
            throw new ArgumentException(
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.WidestSupportedViewportInPixels)} must be a finite "
                + $"positive number of pixels, but was {widest}. Every bound on a star layer is "
                + "measured against it, so a field built on this one would accept any layer at all "
                + "and then draw a band, or nothing.",
                nameof(display));
        }

        if (!IsDrawableExtent(narrowest))
        {
            throw new ArgumentException(
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.NarrowestSupportedViewportInPixels)} must be a finite "
                + $"positive number of pixels, but was {narrowest}. The ceiling on a tile size is "
                + "half of it, so a field built on this one would accept a layer sown so coarsely "
                + "that the screen falls between its stars.",
                nameof(display));
        }

        // Refused rather than left to bite as a pair of bounds that cross: a narrowest screen wider
        // than the widest one raises the floor above the ceiling, so every layer is refused and the
        // advice points both ways at once. The contradiction is in the declaration, so it is named
        // there rather than reported as a fault in the first layer that meets it.
        if (narrowest > widest)
        {
            throw new ArgumentException(
                $"{DisplayOptions.SectionName}:"
                + $"{nameof(DisplayOptions.NarrowestSupportedViewportInPixels)} ({narrowest}) "
                + $"cannot be wider than "
                + $"{nameof(DisplayOptions.WidestSupportedViewportInPixels)} ({widest}). A screen "
                + "cannot be both, and the two bounds a star layer is held between would cross.",
                nameof(display));
        }

        _camera = camera;
        WidestSupportedViewportInPixels = widest;
        NarrowestSupportedViewportInPixels = narrowest;
    }

    /// <summary>
    /// The depths the field is drawn at, furthest first — which is also the order they stack, so
    /// a near star is drawn over a far one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// More than one layer is what makes the field read as depth rather than as a moving texture.
    /// Settable so the field can be sown differently without a new type, and validated here
    /// because a layer with a zero tile size or a parallax of zero fails as a division or as a
    /// field that never moves, neither of which is obvious at the point it was written.
    /// </para>
    /// <para>
    /// <b>A tile size too small is refused too</b>, because it fails in a way that looks like it
    /// worked: the layer draws tens of thousands of stars and still leaves a blank border, since
    /// the viewport spans more tiles than <see cref="MaxTilesPerAxis"/> allows. That is a mistake
    /// in the layer rather than in the zoom, so it belongs where the layer is written.
    /// </para>
    /// <para>
    /// <b>And so is a tile size too large</b>, which is the same failure from the other side: past
    /// <see cref="LargestUsableTileSize"/> the layer draws a handful of stars and the screen can
    /// fall between them, so the player is shown an empty sky by a layer that passed every other
    /// check.
    /// </para>
    /// <para>
    /// <b>Finiteness is checked before any of the bounds, and separately from them.</b> Every
    /// bound below is an ordered comparison, and an ordered comparison against <c>NaN</c> is false
    /// whichever way round it is written — so <c>NaN</c> would satisfy each guard that reads as
    /// though it excludes it, and the layer would then sow a field at a position of <c>NaN</c>:
    /// not a blank border but a blank screen, reached through the documented way to re-sow the
    /// field. Checking each number is finite up front is what makes the bounds mean what they say,
    /// and it also lets the failure name the field that is actually wrong: an infinite
    /// <see cref="StarLayer.SizeInPixels"/> makes <see cref="SmallestUsableTileSize"/> infinite
    /// too, so left to the floor check it would be reported as a tile size to raise.
    /// </para>
    /// <para>
    /// <b>Where the two bounds cross, what is named depends on what crossed them.</b> A star large
    /// enough to push the floor past the ceiling is the star's fault and says so. But the floor
    /// also rises with <see cref="DisplayOptions.WidestSupportedViewportInPixels"/> and the ceiling
    /// falls with <see cref="DisplayOptions.NarrowestSupportedViewportInPixels"/>, so a wide enough
    /// declaration crosses them on its own, with any layer at all. The two are told apart by the
    /// floor under a star of no size — the least this declared screen can ask of a tile — and when
    /// even that clears the ceiling the declaration is named instead. Sending the reader to lower a
    /// three pixel star cannot move a floor set by a hundred thousand pixel screen, and advice that
    /// reads as actionable and is not costs more than none.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A layer has a parallax, a tile size or a star size that is not a finite number; a parallax
    /// outside <c>(0, 1]</c>; a tile size that is not positive, is too small to fill the screen
    /// within <see cref="MaxTilesPerAxis"/> tiles, or is larger than
    /// <see cref="LargestUsableTileSize"/>; fewer than one star per tile; a star size that is
    /// not positive or is so large that no tile size satisfies both bounds; or a declared pair of
    /// screens so far apart that no layer of any size fits between the bounds they set.
    /// </exception>
    public IReadOnlyList<StarLayer> Layers
    {
        get => _layers;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            foreach (StarLayer layer in value)
            {
                // First, and on its own: none of the ordered comparisons below excludes NaN, so
                // every one of them would pass a layer that draws its stars nowhere.
                ThrowIfNotFinite(layer.Parallax, nameof(StarLayer.Parallax));
                ThrowIfNotFinite(layer.TileSizeInWorldUnits, nameof(StarLayer.TileSizeInWorldUnits));
                ThrowIfNotFinite(layer.SizeInPixels, nameof(StarLayer.SizeInPixels));

                if (layer.Parallax is <= 0f or > 1f)
                {
                    throw new ArgumentException(
                        $"A star layer's parallax must be above zero and at most one, but was {layer.Parallax}.",
                        nameof(value));
                }

                if (layer.TileSizeInWorldUnits <= 0f)
                {
                    throw new ArgumentException(
                        $"A star layer's tile size must be positive, but was {layer.TileSizeInWorldUnits}.",
                        nameof(value));
                }

                if (layer.StarsPerTile < 1)
                {
                    throw new ArgumentException(
                        $"A star layer must sow at least one star per tile, but sowed {layer.StarsPerTile}.",
                        nameof(value));
                }

                if (layer.SizeInPixels <= 0f)
                {
                    throw new ArgumentException(
                        $"A star's size must be positive, but was {layer.SizeInPixels}.",
                        nameof(value));
                }

                // Last, because the smallest usable tile size is a function of the star's size,
                // and there is no point deriving it from a size that has just been refused.
                float smallest = SmallestUsableTileSize(layer.SizeInPixels);

                // Where the floor clears the ceiling there is no tile size that would work, so the
                // tile size is not the thing to change and saying it is would send the reader into
                // a wall. Which of the other two it *is* depends on what did the crossing, and the
                // answer is whether the declaration leaves any room at all: the floor under a star
                // of no size is the least this declared screen can ask of a tile, so if even that
                // is past the ceiling, no layer of any kind fits between the two and the star is a
                // bystander. Blaming it there is the same defect at one remove — advice that reads
                // as actionable and is not, since lowering a 3 pixel star cannot move a floor set
                // by a 100,000 pixel screen.
                if (SmallestUsableTileSize(0f) > LargestUsableTileSize)
                {
                    throw new ArgumentException(
                        $"{DisplayOptions.SectionName} declares screens no star layer fits "
                        + $"between: filling {WidestSupportedViewportInPixels} pixels needs tiles "
                        + $"of at least {SmallestUsableTileSize(0f)} even for a star of no size, "
                        + $"which is past the {LargestUsableTileSize} a tile can be and still be "
                        + $"spanned by {NarrowestSupportedViewportInPixels} pixels. Lower "
                        + $"{nameof(DisplayOptions.WidestSupportedViewportInPixels)} or raise "
                        + $"{nameof(DisplayOptions.NarrowestSupportedViewportInPixels)}; no "
                        + "SizeInPixels or TileSizeInWorldUnits would help.",
                        nameof(value));
                }

                if (smallest > LargestUsableTileSize)
                {
                    throw new ArgumentException(
                        $"A star {layer.SizeInPixels} pixels across needs tiles of at least "
                        + $"{smallest} to fill the widest supported screen, which is past the "
                        + $"{LargestUsableTileSize} a tile can be and still be spanned by the "
                        + "narrowest one — no tile size would work. Lower SizeInPixels.",
                        nameof(value));
                }

                if (layer.TileSizeInWorldUnits < smallest)
                {
                    throw new ArgumentException(
                        $"A star layer's tile size must be at least {smallest} to fill a "
                        + $"{WidestSupportedViewportInPixels}-pixel screen within the "
                        + $"{MaxTilesPerAxis} tiles a frame will visit, but was "
                        + $"{layer.TileSizeInWorldUnits}. Raise TileSizeInWorldUnits to at least "
                        + $"{smallest}; a smaller tile draws more stars and still leaves a blank "
                        + "border, because the field is clipped to a band around the camera.",
                        nameof(value));
                }

                if (layer.TileSizeInWorldUnits > LargestUsableTileSize)
                {
                    throw new ArgumentException(
                        $"A star layer's tile size must be at most {LargestUsableTileSize} for a "
                        + $"{NarrowestSupportedViewportInPixels}-pixel screen to be certain of "
                        + $"spanning a whole tile, but was {layer.TileSizeInWorldUnits}. Lower "
                        + $"TileSizeInWorldUnits to at most {LargestUsableTileSize}; a larger tile "
                        + "draws fewer stars and the screen can fall between them, leaving the sky "
                        + "empty.",
                        nameof(value));
                }
            }

            _layers = [.. value];
        }
    }

    /// <summary>
    /// Refuses a number a layer cannot be drawn from, naming the field it came from.
    /// </summary>
    /// <remarks>
    /// The field is named rather than left to the value, because "must be a finite number, but was
    /// NaN" three times over says which rule was broken and not which number to go and change.
    /// </remarks>
    /// <param name="value">The number the layer carried.</param>
    /// <param name="field">The name of the <see cref="StarLayer"/> field it came from.</param>
    static void ThrowIfNotFinite(float value, string field)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentException(
                $"A star layer's {field} must be a finite number, but was {value}. Every bound on "
                + "a layer is an ordered comparison, which this would pass without meaning "
                + "anything, and the layer would then draw a field that is nowhere on the screen.",
                "value"); // the init accessor's implicit parameter, which nameof cannot reach here
        }
    }

    /// <summary>
    /// Whether a number describes an extent something can actually be drawn into: positive, and
    /// finite.
    /// </summary>
    /// <remarks>
    /// Finiteness is asked separately rather than trusted to the comparison, for the same reason
    /// <see cref="Layers"/> asks it first: <c>NaN &lt;= 0f</c> is false, so a guard written as a
    /// range check alone lets a zoom or a viewport of <c>NaN</c> through into arithmetic whose
    /// every result is <c>NaN</c>.
    /// </remarks>
    static bool IsDrawableExtent(float value) => float.IsFinite(value) && value > 0f;

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        Vector2 viewport = renderer.ViewportSize;

        // Nothing is on screen at a zoom of nothing or a viewport of nothing, and both would put
        // the tile arithmetic below through a division that means nothing.
        if (!IsDrawableExtent(_camera.PixelsPerUnit)
            || !IsDrawableExtent(viewport.X)
            || !IsDrawableExtent(viewport.Y))
        {
            return;
        }

        // Loaded on first draw rather than up front: the graphics device the texture belongs to
        // does not exist until the platform host has a window, which is after this is built.
        ITexture texture = _texture ??= renderer.Textures.Load(AssetKey);

        // The origin is the middle of the sprite, so a star sits on its position rather than
        // hanging below and to the right of it.
        Vector2 origin = new(texture.Width / 2f, texture.Height / 2f);

        for (int index = 0; index < _layers.Count; index++)
        {
            RenderLayer(renderer, _layers[index], index, viewport, texture, origin);
        }
    }

    void RenderLayer(
        IRenderer renderer,
        StarLayer layer,
        int seed,
        Vector2 viewport,
        ITexture texture,
        Vector2 origin)
    {
        float pixelsPerUnit = _camera.PixelsPerUnit;

        // Where this layer's own middle has reached. A layer takes only its share of the camera's
        // movement, so a distant one has travelled less than the ship has.
        Vector2 layerCentre = _camera.Target * layer.Parallax;

        // What the layer has *not* taken. Adding it to a star's position and going through the
        // camera lands the star where the layer's lag puts it, which keeps every world-to-screen
        // conversion in the game going through the one transform the ship uses — two transforms
        // that could drift apart is exactly the bug this avoids.
        Vector2 lag = _camera.Target - layerCentre;

        // Half the viewport in world units, widened enough that a star just off the edge is still
        // drawn while part of it would show.
        float margin = layer.SizeInPixels / pixelsPerUnit;
        float halfWidth = viewport.X / (2f * pixelsPerUnit) + margin;
        float halfHeight = viewport.Y / (2f * pixelsPerUnit) + margin;

        (int firstX, int lastX) = TilesCovering(layerCentre.X, halfWidth, layer.TileSizeInWorldUnits);
        (int firstY, int lastY) = TilesCovering(layerCentre.Y, halfHeight, layer.TileSizeInWorldUnits);

        float scale = layer.SizeInPixels / texture.Height;

        for (int tileY = firstY; tileY <= lastY; tileY++)
        {
            for (int tileX = firstX; tileX <= lastX; tileX++)
            {
                for (int star = 0; star < layer.StarsPerTile; star++)
                {
                    // Derived from the tile rather than remembered, so a star stands in the same
                    // place every time the tile is visited however long ago it left the screen.
                    uint placement = Hash(tileX, tileY, star, seed);

                    // Worked out in double and narrowed once, so that a tile a long way from the
                    // origin still places its stars across the whole tile rather than collapsing
                    // them onto its corner as the float steps between positions grow.
                    Vector2 position = new(
                        (float)((tileX + (double)Fraction(placement)) * layer.TileSizeInWorldUnits),
                        (float)((tileY + (double)Fraction(Mix(placement))) * layer.TileSizeInWorldUnits));

                    // Unrotated: a point of light has no orientation to get wrong.
                    renderer.Draw(new Sprite(
                        texture,
                        _camera.WorldToScreen(position + lag, viewport),
                        0f,
                        origin,
                        scale));
                }
            }
        }
    }

    /// <summary>
    /// The tiles a span of <paramref name="halfExtent"/> either side of <paramref name="centre"/>
    /// touches, inclusive at both ends.
    /// </summary>
    /// <remarks>
    /// Rounded outwards, so a tile is visited the moment any part of it is in range — that is
    /// what stops a star at the edge of a tile winking out while it is still on screen.
    /// </remarks>
    static (int First, int Last) TilesCovering(float centre, float halfExtent, float tileSize)
    {
        int first = (int)Math.Floor((centre - halfExtent) / tileSize);
        int last = (int)Math.Floor((centre + halfExtent) / tileSize);
        int middle = (int)Math.Floor(centre / tileSize);

        // Clamped around the middle rather than from one end, so what a wound-out zoom loses it
        // loses evenly on both sides instead of leaving the field lopsided.
        return (
            Math.Max(first, middle - (MaxTilesPerAxis / 2)),
            Math.Min(last, middle + ((MaxTilesPerAxis - 1) / 2)));
    }

    /// <summary>
    /// A value that depends on every part of a star's identity and on nothing else — not on where
    /// the camera is, and not on which frame it is.
    /// </summary>
    static uint Hash(int tileX, int tileY, int star, int layer)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = Mix(hash ^ (uint)tileX);
            hash = Mix(hash ^ (uint)tileY);
            hash = Mix(hash ^ (uint)star);
            return Mix(hash ^ (uint)layer);
        }
    }

    /// <summary>
    /// Stirs a value so that neighbouring inputs land nowhere near each other — without which
    /// adjacent tiles would sow their stars in visibly similar places.
    /// </summary>
    static uint Mix(uint value)
    {
        unchecked
        {
            value *= 2654435761u;
            value ^= value >> 15;
            value *= 2246822519u;
            return value ^ (value >> 13);
        }
    }

    /// <summary>
    /// Reads a hash as a position within its tile: at least zero, and below one.
    /// </summary>
    static float Fraction(uint hash) => (hash >> 8) * (1f / 16_777_216f);
}
