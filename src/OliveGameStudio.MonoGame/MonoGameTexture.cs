using Microsoft.Xna.Framework.Graphics;

namespace OliveGameStudio;

/// <summary>
/// A MonoGame <see cref="Texture2D"/> behind the engine's <see cref="ITexture"/>. The wrapper
/// exists so nothing above the platform layer has to name a MonoGame type, which is what lets
/// screens be drawn in a test with no graphics device present.
/// </summary>
/// <param name="texture">The underlying MonoGame texture.</param>
public sealed class MonoGameTexture(Texture2D texture) : ITexture
{
    /// <summary>
    /// The underlying MonoGame texture, for the renderer to hand to a sprite batch. Deliberately
    /// not on <see cref="ITexture"/>: this is the platform recognising its own, not something
    /// the game is invited to reach through.
    /// </summary>
    public Texture2D Texture { get; } = texture;

    /// <inheritdoc />
    public int Width => Texture.Width;

    /// <inheritdoc />
    public int Height => Texture.Height;

    /// <inheritdoc />
    public void Dispose() => Texture.Dispose();
}
