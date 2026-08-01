using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A pilot that flies at a point: it turns the ship towards <see cref="Target"/> and burns once it
/// is roughly pointing at it.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets a playability test be about a whole campaign rather than about one quest on
/// one axis. A test that asserts a marker is 1,000 units forward proves the content was authored;
/// a test that flies a pilot at it proves the content can be reached. A quest added later at some
/// angle to the forward axis is covered by the same test without anyone rewriting it.
/// </para>
/// <para>
/// Deliberately dumb: no route planning and no braking, because the question it answers is whether
/// a marker is reachable at all, not how gracefully. It steers like a player who points at the
/// thing they want and holds the trigger down.
/// </para>
/// </remarks>
/// <param name="player">The player whose position is being flown.</param>
/// <param name="ship">The ship's physics, read for the direction it is currently pointing.</param>
public sealed class SeekingPilot(Player player, ShipMovement ship) : IShipInput
{
    /// <summary>
    /// How far off course the pilot tolerates before easing the helm, in radians. Full helm covers
    /// roughly 0.04 radians in a 60Hz frame, so easing an order of magnitude wider than that
    /// settles on a heading instead of weaving across it.
    /// </summary>
    const double HelmEasesWithin = 0.4;

    /// <summary>
    /// Gets or sets the point the pilot is flying at. Changing it takes effect on the next frame.
    /// </summary>
    public Position Target { get; set; } = Position.Origin;

    /// <inheritdoc />
    public ShipControls Read()
    {
        double dx = Target.X - player.Position.X;
        double dy = Target.Y - player.Position.Y;

        if (dx == 0 && dy == 0)
        {
            // sitting on it: nothing left to steer towards, and Atan2 of nothing is a direction
            // the ship would turn to for no reason
            return ShipControls.Neutral;
        }

        // headings are measured from the positive Y axis and increase clockwise towards positive
        // X, which is Atan2's arguments the other way round from the usual
        double error = ShortestTurn(Math.Atan2(dx, dy) - ship.Heading);

        return new ShipControls(
            // no thrust while pointing away from the target, so a ship that overshoots comes back
            // rather than burning further out on the turn
            thrust: Math.Abs(error) < Math.PI / 2 ? 1 : 0,
            turn: error / HelmEasesWithin);
    }

    /// <summary>
    /// Reduces a difference between two headings to the shorter way round, in <c>[-π, π]</c>, so a
    /// ship a hair clockwise of its target does not turn the long way to reach it.
    /// </summary>
    static double ShortestTurn(double difference)
    {
        double wrapped = difference % Math.Tau;

        return wrapped switch
        {
            > Math.PI => wrapped - Math.Tau,
            < -Math.PI => wrapped + Math.Tau,
            _ => wrapped,
        };
    }
}
