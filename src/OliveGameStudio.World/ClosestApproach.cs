namespace OliveGameStudio;

/// <summary>
/// How near a journey came to a point, and where along the journey that happened.
/// </summary>
/// <remarks>
/// The two travel together because anything measuring a moving object against a fixed one usually
/// needs both: how close is whether it counts, and where along is <em>when</em> it counted. A
/// caller with two markers to measure against the same journey can only order them if it is told
/// where each was passed — measuring distance alone throws that away, and a rule that depends on
/// the order things were reached cannot be written without it.
/// </remarks>
/// <param name="Distance">
/// The shortest distance in world units between the point and the journey, never negative.
/// </param>
/// <param name="Along">
/// How far into the journey the closest approach happened, as a fraction of its length: 0 at the
/// start, 1 at the end. A journey that went nowhere reports 0, because every part of it is its
/// start.
/// </param>
public readonly record struct ClosestApproach(double Distance, double Along);
