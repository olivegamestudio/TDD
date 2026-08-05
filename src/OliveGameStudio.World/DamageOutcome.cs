namespace OliveGameStudio;

/// <summary>
/// Where a hit went: what was sent back at the attacker, what the shield layer soaked, and what
/// reached the hull. The three add up to the hit that arrived and never to more.
/// </summary>
/// <remarks>
/// <para>
/// Reported rather than acted on, because the model declares the rule and the presentation applies
/// it. <see cref="Reflected"/> is how much damage is travelling back, not who it hits — a ship
/// knows what its shields turned around and has no business knowing what fired at it. Whatever owns
/// the fight is what pairs the number with an attacker.
/// </para>
/// <para>
/// The other two are already spent by the time this is read: the shield and the hull have taken
/// them. They are here so a hit can be shown, counted or reacted to without reading two meters
/// before and after and subtracting.
/// </para>
/// </remarks>
/// <param name="Reflected">
/// How much of the hit was bounced back at whatever fired it. It never landed, so it cost neither
/// the shield nor the hull.
/// </param>
/// <param name="Absorbed">How much the shield layer took, and has already lost.</param>
/// <param name="Dealt">How much reached the hull, and has already come off its health.</param>
public readonly record struct DamageOutcome(double Reflected, double Absorbed, double Dealt);
