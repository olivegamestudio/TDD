namespace OliveGameStudio;

/// <summary>
/// A colour a sprite is drawn with: red, green and blue, plus how much of it shows through.
/// </summary>
/// <remarks>
/// <para>
/// Channels run from 0 to 1 rather than 0 to 255, because that is the range the arithmetic on
/// them is done in — fading a screen to a quarter is multiplying, and a byte would round every
/// step of it.
/// </para>
/// <para>
/// <b>The alpha is <em>not</em> multiplied into the other three.</b> What is held here is the
/// colour as the caller means it: "red, half faded" is <c>(1, 0, 0, 0.5)</c> and not
/// <c>(0.5, 0, 0, 0.5)</c>. Whether the graphics device wants it premultiplied is the platform's
/// business and is done on the way to it, which is the same split the rest of the drawing follows
/// — the model declares what is meant, the presentation applies it.
/// </para>
/// <para>
/// A channel outside 0 to 1 is not refused. It has a defined meaning at the device, which clamps
/// it, and there is no reading of an over-bright channel that could quietly draw the wrong thing.
/// A channel that is not a number is refused, for the reason the camera refuses a target that is
/// not a number: it reaches the device as whatever the conversion happens to produce, draws in
/// full without raising anything, and names neither the sprite nor the frame that produced it.
/// </para>
/// </remarks>
public readonly record struct Colour
{
    /// <summary>
    /// Creates a colour from its four channels.
    /// </summary>
    /// <param name="red">How much red, from 0 to 1.</param>
    /// <param name="green">How much green, from 0 to 1.</param>
    /// <param name="blue">How much blue, from 0 to 1.</param>
    /// <param name="alpha">How much of the colour shows, from 0 — invisible — to 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A channel is not a finite number.</exception>
    public Colour(float red, float green, float blue, float alpha)
    {
        ThrowIfNotFinite(red, nameof(red));
        ThrowIfNotFinite(green, nameof(green));
        ThrowIfNotFinite(blue, nameof(blue));
        ThrowIfNotFinite(alpha, nameof(alpha));

        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    /// <summary>
    /// Opaque white — the colour that draws a texture exactly as it was authored. This is what a
    /// sprite carries until something says otherwise.
    /// </summary>
    public static Colour White => new(1f, 1f, 1f, 1f);

    /// <summary>
    /// How much red, from 0 to 1.
    /// </summary>
    public float Red { get; }

    /// <summary>
    /// How much green, from 0 to 1.
    /// </summary>
    public float Green { get; }

    /// <summary>
    /// How much blue, from 0 to 1.
    /// </summary>
    public float Blue { get; }

    /// <summary>
    /// How much of the colour shows, from 0 — invisible — to 1.
    /// </summary>
    public float Alpha { get; }

    /// <summary>
    /// The same colour, showing by the amount given. This is what a fade is made of, and it is a
    /// method rather than a <c>with</c> expression because the channels are read-only: the
    /// constructor is the one door the check on them stands at.
    /// </summary>
    /// <param name="alpha">How much of the colour shows, from 0 — invisible — to 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="alpha"/> is not a finite number.
    /// </exception>
    public Colour WithAlpha(float alpha) => new(Red, Green, Blue, alpha);

    static void ThrowIfNotFinite(float value, string name)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "A colour channel must be a finite number.");
        }
    }
}
