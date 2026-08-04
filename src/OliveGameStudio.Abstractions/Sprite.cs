using System.Numerics;

namespace OliveGameStudio;

/// <summary>
/// One drawing instruction: a texture placed on the screen. Everything here is already in
/// screen space — a sprite has been through the camera by the time it reaches
/// <see cref="IRenderer.Draw"/>, so the renderer performs no transformation of its own and a
/// test can assert exactly where something was drawn.
/// </summary>
/// <param name="Texture">The texture to draw.</param>
/// <param name="Position">
/// Where <paramref name="Origin"/> lands, in pixels from the top left of the viewport.
/// </param>
/// <param name="Rotation">
/// The clockwise rotation about <paramref name="Origin"/>, in radians. Clockwise because the
/// screen's Y axis points down, so a positive angle turns the way the player sees it turn.
/// </param>
/// <param name="Origin">
/// The point within the texture that <paramref name="Position"/> places and
/// <paramref name="Rotation"/> turns about, in pixels from the texture's top left. The centre
/// for anything that rotates about itself; a corner for anything pinned to one.
/// </param>
/// <param name="Scale">The uniform scale applied to the texture.</param>
public readonly record struct Sprite(
    ITexture Texture,
    Vector2 Position,
    float Rotation,
    Vector2 Origin,
    float Scale)
{
    /// <summary>
    /// The colour the texture is drawn through — a tint, and how much of it shows. Opaque white
    /// until something says otherwise, which draws the texture exactly as it was authored.
    /// </summary>
    /// <remarks>
    /// It sits outside the positional parameters because that is what lets the default be white
    /// rather than the zero a struct would otherwise begin at: a sprite that defaulted to
    /// transparent black would draw nothing, and every caller that never asked for a colour would
    /// have to say so. It is still part of the sprite's value — two sprites differing only in
    /// colour are two different instructions.
    /// </remarks>
    public Colour Colour { get; init; } = Colour.White;
}
