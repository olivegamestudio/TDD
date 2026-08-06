namespace OliveGameStudio;

/// <summary>
/// Somebody the player can fly up to and talk to: where they are, how close counts as within
/// earshot, and what they have to say.
/// </summary>
/// <remarks>
/// <para>
/// One type for every kind of them. A quest giver, a vendor and somebody who only has a line of
/// story differ in <em>what they say and what that does</em>, which is content, not in what they
/// are — so nothing here knows the difference, exactly as the engine ships no game content.
/// </para>
/// <para>
/// Static or dynamic is the presence or absence of a <see cref="NpcPatrol"/>. An NPC that stands
/// still holds none, and one that walks holds the route it walks; there is no third state and no
/// flag that can disagree with the route.
/// </para>
/// </remarks>
public sealed class Npc
{
    /// <summary>
    /// Declares an NPC standing at <paramref name="position"/> with something to say.
    /// </summary>
    /// <param name="id">
    /// What identifies this NPC. An identifier, never translated — the name the player reads is
    /// content, and this is not it.
    /// </param>
    /// <param name="position">Where they are when the world starts.</param>
    /// <param name="conversation">What they say when the player talks to them.</param>
    /// <param name="interactionRange">
    /// How close the player has to be, in world units, before they can be talked to.
    /// </param>
    /// <param name="patrol">
    /// The route they walk, or <c>null</c> for somebody who stands still.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is missing or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="conversation"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="interactionRange"/> is not a finite number, or is not above zero. A range of
    /// zero is one the player can never satisfy — they would have to be at exactly the same point —
    /// so an NPC carrying it is one nobody can talk to and nothing says so; an infinite one is
    /// satisfied everywhere, which makes every NPC in the world the one you are standing next to.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="position"/> is not a place in the world. Every distance measured to it would
    /// be <see cref="double.NaN"/>, and since every comparison against that is false the NPC would
    /// simply never be near anybody.
    /// </exception>
    public Npc(
        string id,
        Position position,
        Conversation conversation,
        double interactionRange,
        NpcPatrol? patrol = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(conversation);

        if (!double.IsFinite(interactionRange))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interactionRange),
                interactionRange,
                "An NPC's interaction range must be a finite number of world units.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interactionRange);

        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            throw new ArgumentException("An NPC must stand somewhere in the world.", nameof(position));
        }

        Id = id;
        Position = position;
        Conversation = conversation;
        InteractionRange = interactionRange;
        Patrol = patrol;
    }

    /// <summary>
    /// Gets what identifies this NPC.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets where they are now.
    /// </summary>
    public Position Position { get; private set; }

    /// <summary>
    /// Gets what they say when talked to.
    /// </summary>
    public Conversation Conversation { get; }

    /// <summary>
    /// Gets how close the player has to be to talk to them, in world units.
    /// </summary>
    /// <remarks>
    /// Stated per NPC rather than once for the game, because how close you have to get to somebody
    /// is a fact about them — a station is not a drone — and it is the kind of thing content tunes.
    /// </remarks>
    public double InteractionRange { get; }

    /// <summary>
    /// Gets the route they walk, or <c>null</c> if they stand still.
    /// </summary>
    public NpcPatrol? Patrol { get; }

    /// <summary>
    /// Gets a value indicating whether they stand still.
    /// </summary>
    public bool IsStatic => Patrol is null;

    /// <summary>
    /// Walks their patrol for one frame. A static NPC is unmoved by it.
    /// </summary>
    /// <param name="frameTime">How much time the frame covers.</param>
    /// <remarks>
    /// Called on every frame the world is running, including the frames the player spends in a
    /// conversation: the world does not pause for the player, so whoever they are not talking to
    /// carries on walking while they do.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameTime"/> is negative.</exception>
    public void Update(TimeSpan frameTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameTime, TimeSpan.Zero);

        if (Patrol is null)
        {
            return;
        }

        Position = Patrol.Advance(Position, frameTime);
    }

    /// <summary>
    /// Answers whether somebody at <paramref name="position"/> is close enough to talk to them.
    /// </summary>
    /// <param name="position">Where the person wanting to talk is standing.</param>
    /// <returns><c>true</c> when they are within <see cref="InteractionRange"/>.</returns>
    /// <remarks>
    /// Measured from where the player <em>is</em>, not swept along the journey their frame covered
    /// — and that is the deliberate opposite of how a quest trigger is measured. A trigger is
    /// something the world does to a player flying past, so a fast ship must not step over it. This
    /// is the player asking to talk to somebody at the moment they ask, and answering it from
    /// anywhere the frame passed would open a conversation with somebody they have already flown
    /// well beyond.
    /// </remarks>
    public bool IsWithinReach(Position position) => Position.DistanceTo(position) <= InteractionRange;
}
