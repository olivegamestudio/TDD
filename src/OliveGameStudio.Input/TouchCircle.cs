namespace OliveGameStudio;

/// <summary>
/// Where one of the overlay's circular controls sits on the screen, and how big it is.
/// </summary>
/// <param name="CentreX">The centre of the circle, in pixels from the left edge of the window.</param>
/// <param name="CentreY">The centre of the circle, in pixels from the top edge of the window.</param>
/// <param name="Radius">How far from that centre the circle reaches, in pixels.</param>
/// <remarks>
/// Placement is stated rather than assumed, because it is a layout decision and layout belongs to
/// whoever is drawing the screen. The overlay reads fingers against whatever it is handed, so the
/// same code serves a phone, a tablet held the other way up, and a test that puts the circles
/// somewhere convenient.
/// </remarks>
public readonly record struct TouchCircle(double CentreX, double CentreY, double Radius)
{
    /// <summary>
    /// Whether the given finger is inside this circle.
    /// </summary>
    /// <param name="touch">The finger to test.</param>
    /// <returns><see langword="true"/> if the finger is within the circle, edge included.</returns>
    /// <remarks>
    /// <para>
    /// A circle with no radius contains nothing, not even the single pixel at its own centre. A
    /// circle that has not been placed is the likeliest way to get one, and treating its centre as
    /// a hit would hand the helm to a finger that is nowhere near a control and then divide its
    /// deflection by zero.
    /// </para>
    /// <para>
    /// A finger the host could not place — a coordinate that is not a number — is outside every
    /// circle, which falls out of the comparison rather than being tested for: an ordered
    /// comparison against <c>NaN</c> is false whichever way round it is written. That is deliberate
    /// and is why the test is written as "within" rather than as "not beyond".
    /// </para>
    /// </remarks>
    public bool Contains(TouchPoint touch)
    {
        if (!(Radius > 0))
        {
            return false;
        }

        double dx = touch.X - CentreX;
        double dy = touch.Y - CentreY;

        return (dx * dx) + (dy * dy) <= Radius * Radius;
    }
}
