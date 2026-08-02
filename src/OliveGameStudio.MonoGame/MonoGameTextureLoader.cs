using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace OliveGameStudio;

/// <summary>
/// Loads textures built by the MonoGame content pipeline, keyed by the asset name the content
/// build produced — <c>ship1</c> for <c>Content/ship1.png</c>.
/// </summary>
/// <remarks>
/// The wrappers are cached rather than the textures: the content manager already returns the
/// same <see cref="Texture2D"/> for a repeated key, so without this every frame that loaded on
/// demand would allocate a new wrapper around the same texture.
/// </remarks>
/// <param name="content">The content manager owning the loaded textures.</param>
public sealed class MonoGameTextureLoader(ContentManager content) : ITextureLoader
{
    readonly Dictionary<string, ITexture> _textures = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ITexture Load(string key)
    {
        if (_textures.TryGetValue(key, out ITexture? loaded))
        {
            return loaded;
        }

        // Not disposed here: the content manager owns what it loads and releases it on unload,
        // so disposing a wrapper would pull a texture out from under anything else holding it.
        loaded = new MonoGameTexture(content.Load<Texture2D>(key));
        _textures.Add(key, loaded);
        return loaded;
    }
}
