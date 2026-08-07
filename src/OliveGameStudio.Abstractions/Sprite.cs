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

    /// <summary>
    /// How far the two axes are scaled relative to each other, on top of <see cref="Scale"/>. One
    /// on both axes — what a sprite carries until something says otherwise — draws the texture at
    /// its authored shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Almost everything drawn keeps its shape, so <see cref="Scale"/> stays the single factor and
    /// this is the exception rather than a second half of it. What needs the exception is anything
    /// sized to the <em>window</em>: a window's aspect is whatever the player dragged it to, so an
    /// overlay covering one cannot be square, and a uniform scale can only either leave two edges
    /// bare or push the other two off the screen.
    /// </para>
    /// <para>
    /// It sits outside the positional parameters for the reason <see cref="Colour"/> does: that is
    /// what lets the default be one on both axes rather than the zero a struct begins at, which
    /// would draw nothing and make every existing caller say so. It is still part of the sprite's
    /// value — two sprites differing only in stretch are two different instructions.
    /// </para>
    /// </remarks>
    public Vector2 Stretch { get; init; } = Vector2.One;
}
