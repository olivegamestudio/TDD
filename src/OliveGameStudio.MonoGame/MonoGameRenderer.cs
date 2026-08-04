using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace OliveGameStudio;

/// <summary>
/// Draws the engine's sprites through a MonoGame <see cref="SpriteBatch"/>.
/// </summary>
/// <remarks>
/// This is the whole of the platform's drawing code, and it is deliberately thin: it translates
/// a <see cref="Sprite"/> into a sprite batch call and decides nothing. Nothing here can be
/// covered by a test — a sprite batch needs a real graphics device — so anything that could be
/// got wrong belongs above it, where a substituted <see cref="IRenderer"/> can see it.
/// </remarks>
public sealed class MonoGameRenderer : IRenderer, IDisposable
{
    readonly GraphicsDevice _device;
    readonly SpriteBatch _spriteBatch;

    /// <summary>
    /// Creates a renderer over a graphics device and the content loaded for it.
    /// </summary>
    /// <param name="device">The graphics device to draw with.</param>
    /// <param name="textures">The textures available to draw.</param>
    public MonoGameRenderer(GraphicsDevice device, ITextureLoader textures)
    {
        _device = device;
        _spriteBatch = new SpriteBatch(device);
        Textures = textures;
    }

    /// <inheritdoc />
    public ITextureLoader Textures { get; }

    /// <inheritdoc />
    public Vector2 ViewportSize => new(_device.Viewport.Width, _device.Viewport.Height);

    /// <summary>
    /// Opens the frame's batch. Every <see cref="Draw"/> for the frame falls between this and
    /// <see cref="EndFrame"/>; the platform host owns the pairing, because it owns the frame.
    /// </summary>
    public void BeginFrame() => _spriteBatch.Begin();

    /// <inheritdoc />
    public void Draw(Sprite sprite)
    {
        if (sprite.Texture is not MonoGameTexture texture)
        {
            throw new ArgumentException(
                $"Expected a texture loaded by this platform, but got '{sprite.Texture.GetType()}'.",
                nameof(sprite));
        }

        _spriteBatch.Draw(
            texture.Texture,
            new XnaVector2(sprite.Position.X, sprite.Position.Y),
            sourceRectangle: null,
            ToXna(sprite.Colour),
            sprite.Rotation,
            new XnaVector2(sprite.Origin.X, sprite.Origin.Y),
            sprite.Scale,
            SpriteEffects.None,
            layerDepth: 0f);
    }

    /// <summary>
    /// Closes the frame's batch, submitting everything drawn since <see cref="BeginFrame"/>.
    /// </summary>
    public void EndFrame() => _spriteBatch.End();

    /// <summary>
    /// The engine's colour as this device wants it: premultiplied.
    /// </summary>
    /// <remarks>
    /// <see cref="SpriteBatch.Begin"/> blends with <see cref="BlendState.AlphaBlend"/> unless told
    /// otherwise, which since XNA 4 is <em>premultiplied</em> alpha, and the content pipeline
    /// premultiplies the textures to match. A tint handed over straight would therefore be blended
    /// as though it were already multiplied, and a half-faded sprite would come out too bright
    /// rather than half faded. Multiplying here is what <c>Color.White * alpha</c> does in every
    /// MonoGame sample, stated once in the one place the platform is known rather than left to
    /// each caller to remember. <see cref="Colour"/> itself holds the colour as the caller means
    /// it.
    /// </remarks>
    static XnaColor ToXna(Colour colour) => new(
        colour.Red * colour.Alpha,
        colour.Green * colour.Alpha,
        colour.Blue * colour.Alpha,
        colour.Alpha);

    /// <inheritdoc />
    public void Dispose() => _spriteBatch.Dispose();
}
