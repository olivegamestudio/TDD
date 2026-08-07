namespace OliveGameStudio;

/// <summary>
/// One frame of the touch screen, as the platform host read it: every finger that is down.
/// </summary>
/// <remarks>
/// <para>
/// Every finger rather than one, because touch is the only device here that can be doing two
/// unrelated things at once. A keyboard reports the same keys however many hands are on it, but a
/// thumb on the helm and a thumb on the trigger are two controls being worked in parallel, and a
/// frame that carried only the first finger down would make flying and firing at the same time
/// impossible to express.
/// </para>
/// <para>
/// <see cref="KeyboardFrame"/> and <see cref="GamePadFrame"/> are made of values a host cannot
/// leave unset. This one carries a list, so it takes the trouble to have no unset state either:
/// <see cref="Points"/> answers an empty list for a frame nobody filled in, because it is read on
/// the frame path where there is no room to check whether the platform bothered.
/// </para>
/// </remarks>
public readonly record struct TouchFrame
{
    readonly IReadOnlyList<TouchPoint>? _points;

    /// <summary>
    /// Reports the fingers that are down this frame.
    /// </summary>
    /// <param name="points">
    /// Every finger currently on the screen, or <see langword="null"/> for none — which a host
    /// with no touch screen, or nobody touching it, may report either way.
    /// </param>
    /// <remarks>
    /// The list is taken as given rather than copied. The host reads its devices once a frame and
    /// hands the reading straight over, so a host that then edits the list it passed would be
    /// changing what the game already saw — the same rule that applies to every other frame in
    /// this seam, which are value types and cannot be edited behind the game's back at all.
    /// </remarks>
    public TouchFrame(IReadOnlyList<TouchPoint>? points) => _points = points;

    /// <summary>
    /// A screen nobody is touching. What a host with no touch screen reports, and what the tests
    /// start from.
    /// </summary>
    public static readonly TouchFrame None = default;

    /// <summary>
    /// Gets every finger that is down this frame, in the order the host reported them. Never
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The order is the host's and means nothing in itself, beyond being the order in which two
    /// fingers that both landed on the same control this frame will be considered.
    /// </remarks>
    public IReadOnlyList<TouchPoint> Points => _points ?? [];
}
