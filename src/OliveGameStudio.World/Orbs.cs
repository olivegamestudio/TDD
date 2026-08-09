namespace OliveGameStudio;

/// <summary>
/// What is in a ship's two additional slots, and where the companion objects in them are.
/// </summary>
/// <remarks>
/// <para>
/// The slots are filled freely: any orb can go in either, exactly as the shield slots are.
/// <b>Nothing adds up, though</b>, and that is the difference worth stating — an orb acts on its
/// own, so two of a kind is two companions each doing its own thing rather than one effect at
/// double strength. There is no total to read here because there is no quantity two orbs share.
/// </para>
/// <para>
/// The slot count is fixed here rather than being a ship stat because v1 hulls all carry the same
/// layout, which is the same reason <see cref="Shielding.Slots"/> is. When a hull is allowed to
/// vary it, <see cref="Slots"/> becomes a number the hull supplies and this type takes it.
/// </para>
/// <para>
/// Immutable, and it holds no orb's position. <see cref="PlaceAround"/> works out where the
/// companions are from what was authored and how long has been flown, so nothing here is state that
/// could fall out of step with the ship, and a frame drawn twice draws them in the same place both
/// times.
/// </para>
/// </remarks>
public sealed class Orbs
{
    /// <summary>
    /// How many orbs a ship can carry at once.
    /// </summary>
    public const int Slots = 2;

    readonly OrbStats[] _fitted;

    /// <summary>
    /// Fills the additional slots.
    /// </summary>
    /// <param name="fitted">
    /// The orbs to fit, at most <see cref="Slots"/> of them. Fewer is an ordinary answer — every
    /// ship starts a new game with the slots empty.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="fitted"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// More orbs than there are slots, or a slot holding nothing. An empty slot is expressed by
    /// fitting fewer orbs, so a <c>null</c> among them is a caller that lost one rather than a
    /// caller leaving a slot free.
    /// </exception>
    public Orbs(params OrbStats[] fitted)
    {
        ArgumentNullException.ThrowIfNull(fitted);

        if (fitted.Length > Slots)
        {
            throw new ArgumentException($"A ship has {Slots} additional slots.", nameof(fitted));
        }

        if (Array.Exists(fitted, orb => orb is null))
        {
            throw new ArgumentException("An additional slot holds an orb or nothing at all.", nameof(fitted));
        }

        // copied rather than kept, so a caller that goes on to reuse its array cannot change what a
        // ship is carrying after it has been fitted
        _fitted = [.. fitted];
    }

    /// <summary>
    /// Gets the empty slots a ship carries before it has found an orb.
    /// </summary>
    public static Orbs None { get; } = new();

    /// <summary>
    /// Gets the orbs fitted, in the order they were slotted.
    /// </summary>
    public IReadOnlyList<OrbStats> Fitted => _fitted;

    /// <summary>
    /// Works out where each fitted orb is, given where the ship is and how long has been flown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole of "auto-controlled — no manual fire input": an orb's place is a function
    /// of what it was authored with and how long the game has run, and of nothing the player
    /// pressed. Nothing has to be ticked and nothing accumulates, so the answer cannot drift with
    /// the frame rate and a resumed game does not have to remember where the companions had got to.
    /// </para>
    /// <para>
    /// Each slot takes an even share of the ring, so the two orbs a ship can carry start on opposite
    /// sides of it. Slots are filled freely and two of a kind is the ordinary case rather than the
    /// exotic one, and without a share each they would occupy the same point at every instant — a
    /// player who fitted two would see one.
    /// </para>
    /// <para>
    /// A bearing is measured the way a ship's heading is — zero along the positive world Y axis,
    /// increasing to starboard — so this agrees with the convention the drawing side already asks of
    /// the ship and nothing has to convert to place an orb beside one. The ring is measured in world
    /// terms rather than turning with the hull: a companion object runs itself, so it keeps its own
    /// bearing while the ship turns under it rather than being dragged round like something bolted
    /// on.
    /// </para>
    /// </remarks>
    /// <param name="ship">Where the ship is. The ring is measured from here, so the companions
    /// travel with it.</param>
    /// <param name="secondsFlown">
    /// How long has been flown, in seconds. It is the elapsed total rather than a frame's share of
    /// it, which is what keeps this from having to remember anything.
    /// </param>
    /// <returns>
    /// Where each fitted orb is, in the order the orbs were slotted. Empty when the slots are.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="secondsFlown"/> is negative or not a finite number. The sine of a time that
    /// is not a number is not a number, so every orb would be placed nowhere — in full, raising
    /// nothing — which is the failure the camera's own guards exist to turn into a stack trace.
    /// Time before the game began is not a shorter answer to where an orb is; it is a caller that
    /// subtracted the wrong way round.
    /// <para>
    /// Also when <paramref name="secondsFlown"/> and a fitted orb's
    /// <see cref="OrbStats.AngularSpeed"/> are each finite but multiply past what a double holds.
    /// That is the same orb-placed-nowhere failure reached by two values that each pass their own
    /// guard, so it is refused on the same terms rather than returned as a position of
    /// <see cref="double.NaN"/>. A merely large bearing is not refused: a bearing is periodic, so
    /// going round a great many times is an ordinary answer.
    /// </para>
    /// </exception>
    public IReadOnlyList<Position> PlaceAround(Position ship, double secondsFlown)
    {
        if (!double.IsFinite(secondsFlown))
        {
            throw new ArgumentOutOfRangeException(
                nameof(secondsFlown),
                secondsFlown,
                "An orb can only be placed at a time that is a finite number of seconds.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(secondsFlown, nameof(secondsFlown));

        Position[] placed = new Position[_fitted.Length];

        for (int slot = 0; slot < _fitted.Length; slot++)
        {
            OrbStats orb = _fitted[slot];
            double bearing = (slot * 2 * Math.PI / Slots) + (orb.AngularSpeed * secondsFlown);

            // The rate is finite and the time is finite, and their product still need not be: two
            // large finite numbers multiply past what a double holds and come back as infinity. The
            // sine of that is NaN, so this is the orb-placed-nowhere failure the guard above already
            // refuses a NaN time for, arriving one line later — and a NaN position is worse than a
            // loud one, because every distance check and drawing transform downstream quietly turns
            // NaN as well and nothing reports where it started.
            if (!double.IsFinite(bearing))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(secondsFlown),
                    secondsFlown,
                    $"An orb turning at {orb.AngularSpeed} radians a second has no bearing after this "
                        + "long: the rate and the time multiply past what a number can hold.");
            }

            placed[slot] = ship.Offset(
                orb.Radius * Math.Sin(bearing),
                orb.Radius * Math.Cos(bearing));
        }

        return placed;
    }
}
