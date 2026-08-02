using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// An <see cref="IRenderer"/> that draws nothing and remembers everything. This is the whole
/// test story for drawing: what a screen puts on screen is a list of sprites, and a list of
/// sprites can be asserted on without a window, a graphics device or a human looking at it.
/// </summary>
public sealed class RecordingRenderer(float width = 800f, float height = 600f) : IRenderer
{
    readonly List<Sprite> _drawn = [];

    public RecordingTextureLoader Textures { get; } = new();

    ITextureLoader IRenderer.Textures => Textures;

    public Vector2 ViewportSize { get; set; } = new(width, height);

    /// <summary>
    /// The middle of the viewport, where a camera puts whatever it is following.
    /// </summary>
    public Vector2 ViewportCentre => ViewportSize / 2f;

    /// <summary>
    /// Everything drawn since the last <see cref="Clear"/>, in the order it was drawn — which is
    /// the order it stacks on screen.
    /// </summary>
    public IReadOnlyList<Sprite> Drawn => _drawn;

    /// <summary>
    /// The only sprite drawn, failing the test if there was not exactly one.
    /// </summary>
    public Sprite Single() => Assert.Single(_drawn);

    public void Draw(Sprite sprite) => _drawn.Add(sprite);

    /// <summary>
    /// Forgets what was drawn, so a second frame can be asserted on by itself.
    /// </summary>
    public void Clear() => _drawn.Clear();
}

/// <summary>
/// A texture loader that hands out fake textures of a known size and counts what was asked for,
/// so a drawable that loads on first use can be held to loading once.
/// </summary>
public sealed class RecordingTextureLoader : ITextureLoader
{
    readonly Dictionary<string, FakeTexture> _textures = new(StringComparer.Ordinal);

    /// <summary>
    /// The size handed out for any key not given one by <see cref="SetSize"/>.
    /// </summary>
    public (int Width, int Height) DefaultSize { get; set; } = (1024, 1024);

    readonly Dictionary<string, (int Width, int Height)> _sizes = new(StringComparer.Ordinal);

    /// <summary>
    /// Every key that has been asked for, including repeats.
    /// </summary>
    public List<string> Requested { get; } = [];

    public void SetSize(string key, int width, int height) => _sizes[key] = (width, height);

    public ITexture Load(string key)
    {
        Requested.Add(key);

        if (_textures.TryGetValue(key, out FakeTexture? texture))
        {
            return texture;
        }

        (int width, int height) = _sizes.TryGetValue(key, out (int Width, int Height) size) ? size : DefaultSize;
        texture = new FakeTexture(width, height);
        _textures.Add(key, texture);
        return texture;
    }
}

/// <summary>
/// A texture with a size and nothing behind it.
/// </summary>
public sealed class FakeTexture(int width, int height) : ITexture
{
    public int Width => width;

    public int Height => height;

    public void Dispose()
    {
    }
}
