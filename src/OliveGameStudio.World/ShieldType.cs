namespace OliveGameStudio;

/// <summary>
/// What a shield does with a hit.
/// </summary>
/// <remarks>
/// The type is what a shield <em>is</em>, not a setting on it: the two answers are not two
/// strengths of the same behaviour. An absorption shield takes the hit so the hull does not; a
/// reflecting shield never takes it at all. Which is why <see cref="ShieldStats"/> is built through
/// a named factory per type rather than assembled field by field — there is no such thing as a
/// shield that is a bit of both.
/// </remarks>
public enum ShieldType
{
    /// <summary>
    /// Soaks damage: the layer that depletes before health does.
    /// </summary>
    Absorption,

    /// <summary>
    /// Bounces its share of an incoming hit back at whatever fired it. The reflected share never
    /// lands, so it costs neither the shield layer nor the hull.
    /// </summary>
    Reflection,
}
