using System.Numerics;
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
/// <param name="camera">The camera the world is drawn through — the same one the ship uses.</param>
/// <param name="display">
/// The display the game states it supports, which is what a layer's tile size is held against.
/// Left null, the declared default applies — the field is not the place a supported display gets
/// decided, so it reads <see cref="DisplayOptions"/> either way rather than keeping a number of
/// its own.
/// </param>
public sealed class StarField(ICamera camera, DisplayOptions? display = null) : IRenderable
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
    /// to fill <see cref="WidestSupportedViewportInPixels"/> — the display
    /// <see cref="DisplayOptions"/> declares — at one pixel per unit, so the half that is a
    /// mistake in the layer fails where the layer is written. The other half cannot be prevented
    /// by any bound on tile size, because a low enough <see cref="ICamera.PixelsPerUnit"/> spans
    /// this many tiles whatever their size.
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
    /// taken against a stated screen. This is that screen, and it is
    /// <see cref="DisplayOptions.WidestSupportedViewportInPixels"/> — read, not restated. Which
    /// displays the game supports is a decision about the game rather than about stars, so it is
    /// made where the game states its other facts and the field derives its floor from it. The
    /// check therefore rejects a tile size that could never fill a supported screen rather than
    /// one that merely might not fill some particular screen.
    /// </para>
    /// <para>
    /// <b>It moves when the declaration moves.</b> Configure a wider display and the floor under
    /// every tile size rises with it, so a layer that was fine against a narrower declaration is
    /// refused rather than quietly leaving a band. That is the point of deriving it: the two
    /// numbers cannot drift apart, because there is only one of them. Raising it is safe for the
    /// shipping layers — see <see cref="DisplayOptions.DefaultWidestSupportedViewportInPixels"/>
    /// for how much headroom they have.
    /// </para>
    /// </remarks>
    public float WidestSupportedViewportInPixels => _display.WidestSupportedViewportInPixels;

    /// <summary>
    /// The smallest tile size a layer drawing stars <paramref name="sizeInPixels"/> across can be
    /// sown at and still cover <paramref name="widestSupportedViewportInPixels"/> within
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
    /// Takes the screen as an argument rather than reading the declared one, so the relationship
    /// between a display and the floor it implies can be stated — and tested — without a field to
    /// hang it on. <see cref="SmallestUsableTileSize(float)"/> is the same sum against the display
    /// this field was built for, and is what <see cref="Layers"/> validates against.
    /// </para>
    /// </remarks>
    /// <param name="sizeInPixels">How big the layer draws each star, in pixels.</param>
    /// <param name="widestSupportedViewportInPixels">
    /// The widest screen the layer has to fill, in pixels at one pixel per unit.
    /// </param>
    /// <returns>The smallest tile size, in world units, that fills that screen.</returns>
    public static float SmallestUsableTileSize(float sizeInPixels, float widestSupportedViewportInPixels) =>
        (widestSupportedViewportInPixels + (2f * sizeInPixels)) / (MaxTilesPerAxis - 4f);

    /// <summary>
    /// The smallest tile size a layer drawing stars <paramref name="sizeInPixels"/> across can be
    /// sown at and still cover <see cref="WidestSupportedViewportInPixels"/> — the display this
    /// field was built for — within <see cref="MaxTilesPerAxis"/> tiles.
    /// </summary>
    /// <param name="sizeInPixels">How big the layer draws each star, in pixels.</param>
    /// <returns>The smallest tile size, in world units, that fills the supported screen.</returns>
    public float SmallestUsableTileSize(float sizeInPixels) =>
        SmallestUsableTileSize(sizeInPixels, WidestSupportedViewportInPixels);

    static readonly StarLayer[] _defaultLayers =
    [
        // Far to near, which is also the order they are drawn in: the near layer stacks over the
        // far one. The nearest is fixed in the world, so it slides past at exactly the speed the
        // ship is flying, and the others lag by their share of it.
        new StarLayer(Parallax: 0.30f, TileSizeInWorldUnits: 90f, StarsPerTile: 2, SizeInPixels: 2.5f),
        new StarLayer(Parallax: 0.60f, TileSizeInWorldUnits: 150f, StarsPerTile: 2, SizeInPixels: 3.5f),
        new StarLayer(Parallax: 1.00f, TileSizeInWorldUnits: 230f, StarsPerTile: 2, SizeInPixels: 5f),
    ];

    // Held rather than read through the parameter each time, so the field answers with the same
    // display for its whole life even though the options object it came from is shared.
    readonly DisplayOptions _display = display ?? new DisplayOptions();

    readonly IReadOnlyList<StarLayer> _layers = _defaultLayers;

    ITexture? _texture;

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
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A layer has a parallax outside <c>(0, 1]</c>, a tile size that is not positive or is too
    /// small to fill the screen within <see cref="MaxTilesPerAxis"/> tiles, fewer than one star
    /// per tile, or a size that is not positive.
    /// </exception>
    public IReadOnlyList<StarLayer> Layers
    {
        get => _layers;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            foreach (StarLayer layer in value)
            {
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
                float smallest = SmallestUsableTileSize(layer.SizeInPixels, WidestSupportedViewportInPixels);

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
            }

            _layers = [.. value];
        }
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer)
    {
        Vector2 viewport = renderer.ViewportSize;

        // Nothing is on screen at a zoom of nothing or a viewport of nothing, and both would put
        // the tile arithmetic below through a division that means nothing.
        if (camera.PixelsPerUnit <= 0f || viewport.X <= 0f || viewport.Y <= 0f)
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
        float pixelsPerUnit = camera.PixelsPerUnit;

        // Where this layer's own middle has reached. A layer takes only its share of the camera's
        // movement, so a distant one has travelled less than the ship has.
        Vector2 layerCentre = camera.Target * layer.Parallax;

        // What the layer has *not* taken. Adding it to a star's position and going through the
        // camera lands the star where the layer's lag puts it, which keeps every world-to-screen
        // conversion in the game going through the one transform the ship uses — two transforms
        // that could drift apart is exactly the bug this avoids.
        Vector2 lag = camera.Target - layerCentre;

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
                        camera.WorldToScreen(position + lag, viewport),
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
