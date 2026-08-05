namespace OliveGameStudio;

/// <summary>
/// What a companion object does once it is in an additional slot.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour is what an orb <em>is</em>, not a setting on it, for the same reason
/// <see cref="ShieldType"/> is: the two answers are not two strengths of one thing. One holds a
/// ring around the ship and never leaves it; the other leaves station to go after something. Which
/// is why <see cref="OrbStats"/> is built through a named factory per behaviour rather than
/// assembled field by field — there is no such thing as an orb that is a bit of both.
/// </para>
/// <para>
/// The design also names "a ball" as a third kind of orb, and it is deliberately absent: what a
/// ball <em>does</em> is not stated anywhere, and a member here with no behaviour behind it is a
/// value every caller has to handle and none can act on.
/// </para>
/// </remarks>
public enum OrbBehaviour
{
    /// <summary>
    /// Circles the ship — an orbiting defence, always at its authored distance and never anywhere
    /// else.
    /// </summary>
    Orbiting,

    /// <summary>
    /// Leaves station to go after a target — an autonomous attacker. It holds its place in the ring
    /// while there is nothing to go after, which today is always: nothing in the world is hostile
    /// yet, so what a tracker chases and what it costs whatever it catches belong with the fight.
    /// </summary>
    Tracking,
}
