namespace OliveGameStudio;

/// <summary>
/// A route an NPC walks: the waypoints it visits, in order and on a loop, and how fast it covers
/// them.
/// </summary>
/// <remarks>
/// <para>
/// A patrol carries where it has got to, so it belongs to one NPC and cannot be shared between
/// two — handing the same patrol to a pair of NPCs would have them pulling the same waypoint back
/// and forth. <see cref="Npc"/> takes one and nothing else does.
/// </para>
/// <para>
/// It moves along the <em>whole</em> of the time it is given rather than one waypoint per frame:
/// a long frame that would carry the NPC past several waypoints follows the route round them
/// instead of cutting the corner. That is the same rule the quest triggers are swept under — what
/// happens must not depend on where the frames happened to fall.
/// </para>
/// </remarks>
public sealed class NpcPatrol
{
    readonly Position[] _waypoints;

    int _next;

    /// <summary>
    /// Declares a patrol at <paramref name="speed"/> around <paramref name="waypoints"/>.
    /// </summary>
    /// <param name="speed">How fast the NPC walks it, in world units a second.</param>
    /// <param name="waypoints">Where it goes, in order. It returns to the first after the last.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="speed"/> is not a finite number, or is not above zero. A patrol that does
    /// not move is a static NPC, which is said by giving it no patrol at all rather than by giving
    /// it one that goes nowhere.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="waypoints"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="waypoints"/> is empty, or holds a coordinate that is not a finite number. A
    /// route to nowhere cannot be walked, and one waypoint that is not a number would take the NPC
    /// somewhere nothing can measure a distance to — including whatever asks how close the player
    /// is standing.
    /// </exception>
    public NpcPatrol(double speed, IEnumerable<Position> waypoints)
    {
        // ordered before the range guard so NaN and negative infinity are named by the rule they
        // break rather than caught incidentally by a comparison that is false for every NaN
        if (!double.IsFinite(speed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "A patrol's speed must be a finite number of world units a second.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speed);
        ArgumentNullException.ThrowIfNull(waypoints);

        _waypoints = [.. waypoints];

        if (_waypoints.Length == 0)
        {
            throw new ArgumentException("A patrol must have at least one waypoint.", nameof(waypoints));
        }

        if (Array.Exists(_waypoints, w => !double.IsFinite(w.X) || !double.IsFinite(w.Y)))
        {
            throw new ArgumentException(
                "A patrol's waypoints must all be places in the world.",
                nameof(waypoints));
        }

        Speed = speed;
    }

    /// <summary>
    /// Gets how fast the route is walked, in world units a second.
    /// </summary>
    public double Speed { get; }

    /// <summary>
    /// Gets the waypoints, in the order they are visited.
    /// </summary>
    public IReadOnlyList<Position> Waypoints => _waypoints;

    /// <summary>
    /// Gets the waypoint being walked to now.
    /// </summary>
    public Position Next => _waypoints[_next];

    /// <summary>
    /// Walks the route for one frame and answers where that left the walker.
    /// </summary>
    /// <param name="from">Where the walker stands as the frame begins.</param>
    /// <param name="frameTime">How much time the frame covers.</param>
    /// <returns>Where the walker stands once the frame's time has been spent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="frameTime"/> is negative. Time does not run backwards, and a negative frame
    /// would walk the route in reverse past waypoints it never reached.
    /// </exception>
    public Position Advance(Position from, TimeSpan frameTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameTime, TimeSpan.Zero);

        double remaining = Speed * frameTime.TotalSeconds;

        if (!double.IsFinite(remaining))
        {
            // Only reachable from a frame of a length no frame loop produces, at a speed content
            // stated — but the walk below spends the distance it is given, so an infinite one is
            // never spent and the route is walked forever. Refused here, where the two numbers
            // first meet, rather than left to hang the frame that happened to be handed it.
            throw new ArgumentOutOfRangeException(
                nameof(frameTime),
                frameTime,
                "A frame this long at this speed covers a distance that is not a number of world units.");
        }

        Position at = from;

        // A frame long enough to cross several waypoints keeps walking, and the counter is what
        // stops a route whose waypoints all stand on the same spot from spinning here forever:
        // every waypoint reached without spending any distance is one step towards giving up, and
        // reaching one that did cost something proves the route goes somewhere and resets it.
        int stalled = 0;

        while (remaining > 0 && stalled <= _waypoints.Length)
        {
            double toNext = at.DistanceTo(Next);

            if (toNext > remaining)
            {
                double fraction = remaining / toNext;
                return new Position(
                    at.X + ((Next.X - at.X) * fraction),
                    at.Y + ((Next.Y - at.Y) * fraction));
            }

            at = Next;
            remaining -= toNext;
            stalled = toNext > 0 ? 0 : stalled + 1;

            _next = (_next + 1) % _waypoints.Length;
        }

        return at;
    }
}
