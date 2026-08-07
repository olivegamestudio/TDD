namespace OliveGameStudio;

/// <summary>
/// The two circular controls a touch screen is played with: a stick on the left that flies the
/// ship, and a button on the right that fires.
/// </summary>
/// <remarks>
/// <para>
/// The overlay is drawn over the same heads-up display everything else uses rather than replacing
/// it, so there is one screen layout and not a second mobile one. This class is the reading half
/// of that: it knows where the two circles are and turns the fingers on them into the same
/// <see cref="ShipControls"/> a stick produces, so nothing downstream of it learns that the game
/// is being played by thumb.
/// </para>
/// <para>
/// It keeps one thing between frames — which finger is holding the helm — and it has to. A
/// virtual stick has no physical stop for a thumb to find, so a player pushing for full ahead
/// slides off the circle without feeling it. The helm therefore <em>captures</em> the finger that
/// takes it and follows that finger wherever it goes, until it lifts. The deflection is still
/// measured from the circle's centre, so the circle drawn on the screen still says exactly what
/// the numbers mean.
/// </para>
/// <para>
/// Firing is not captured, because it is a button rather than a stick: a finger that slides off it
/// has stopped pressing it, which is what letting go of a button looks like everywhere else.
/// </para>
/// <para>
/// No dead zone, deliberately. The dead zone the pad needs is there because a used stick does not
/// return to dead centre and would otherwise fly the ship by itself; a finger is where it is put
/// and, when it is lifted, is not there at all. Applying one here would cost the player the middle
/// of the circle for nothing.
/// </para>
/// </remarks>
public sealed class TouchOverlay
{
    readonly TouchCircle _helm;
    readonly TouchCircle _fire;

    /// <summary>
    /// The finger holding the helm, or <see langword="null"/> if nobody is.
    /// </summary>
    /// <remarks>
    /// The identifier rather than the position, because the position is read fresh from every
    /// frame. This is only the answer to "whose thumb is it", which the frame cannot give: two
    /// fingers on one circle are the same two coordinates whichever of them arrived first.
    /// </remarks>
    int? _steering;

    /// <summary>
    /// Places the overlay's two circles.
    /// </summary>
    /// <param name="helm">The left-hand circle, which flies the ship.</param>
    /// <param name="fire">The right-hand circle, which fires.</param>
    public TouchOverlay(TouchCircle helm, TouchCircle fire)
    {
        _helm = helm;
        _fire = fire;
    }

    /// <summary>
    /// Reads one frame of touch.
    /// </summary>
    /// <param name="frame">Every finger on the screen, as the platform host read it.</param>
    /// <returns>What those fingers are asking for.</returns>
    /// <remarks>
    /// Called once a frame. Reading the same frame twice gives the same answer: the capture only
    /// changes when the fingers do, so a fixed-step update that runs twice on one reading does not
    /// hand the helm to somebody else in between.
    /// </remarks>
    public TouchControls Read(TouchFrame frame)
    {
        IReadOnlyList<TouchPoint> touches = frame.Points;

        return new TouchControls(Steering(touches), IsFiring(touches));
    }

    /// <summary>
    /// What the helm circle is being asked for, keeping or taking the finger that holds it.
    /// </summary>
    ShipControls Steering(IReadOnlyList<TouchPoint> touches)
    {
        TouchPoint? holding = Held(touches) ?? Taking(touches);

        _steering = holding?.Id;

        return holding is { } thumb ? Deflection(thumb) : ShipControls.Neutral;
    }

    /// <summary>
    /// The finger that already holds the helm, if it is still down.
    /// </summary>
    /// <remarks>
    /// Found by identifier and not by where it is, which is the point of the capture: this is what
    /// keeps a thumb that has been pushed past the rim flying the ship instead of dropping the
    /// engine at the moment the player asked for all of it.
    /// </remarks>
    TouchPoint? Held(IReadOnlyList<TouchPoint> touches)
    {
        if (_steering is not { } id)
        {
            return null;
        }

        foreach (TouchPoint touch in touches)
        {
            if (touch.Id == id)
            {
                return touch;
            }
        }

        return null;
    }

    /// <summary>
    /// The finger taking a free helm: the first one the host reported inside the circle.
    /// </summary>
    /// <remarks>
    /// Only ever consulted when the helm is free, so a second finger landing on the circle cannot
    /// snatch it from a player mid-turn — a palm, a second hand, or the other thumb straying over
    /// changes nothing until the finger holding it lifts.
    /// </remarks>
    TouchPoint? Taking(IReadOnlyList<TouchPoint> touches)
    {
        foreach (TouchPoint touch in touches)
        {
            if (_helm.Contains(touch))
            {
                return touch;
            }
        }

        return null;
    }

    /// <summary>
    /// How far, and which way, the helm has been pushed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured from the circle's centre and divided by its radius, so the rim is full deflection
    /// however big the circle is drawn. Screen y counts downwards and ahead is up, so the thrust
    /// axis is the one that changes sign.
    /// </para>
    /// <para>
    /// Past the rim the deflection is scaled back onto it rather than clamped axis by axis. Both
    /// answers stop the player asking for more engine than the ship has, but clamping each axis on
    /// its own would quietly turn the ship: a thumb dragged straight up and off the circle is
    /// asking to fly straight, and scaling the pair together is what keeps saying so.
    /// </para>
    /// </remarks>
    ShipControls Deflection(TouchPoint thumb)
    {
        double x = (thumb.X - _helm.CentreX) / _helm.Radius;
        double y = (thumb.Y - _helm.CentreY) / _helm.Radius;

        double reach = Math.Sqrt((x * x) + (y * y));

        if (reach > 1)
        {
            x /= reach;
            y /= reach;
        }

        return new ShipControls(-y, x);
    }

    /// <summary>
    /// Whether the fire circle is being held by anything.
    /// </summary>
    /// <remarks>
    /// Any finger will do, and it is not remembered between frames. A button does not care which
    /// finger is on it, and a player who lands a second finger on it while the first is still down
    /// is still asking for the same one thing.
    /// </remarks>
    bool IsFiring(IReadOnlyList<TouchPoint> touches)
    {
        foreach (TouchPoint touch in touches)
        {
            if (_fire.Contains(touch))
            {
                return true;
            }
        }

        return false;
    }
}
