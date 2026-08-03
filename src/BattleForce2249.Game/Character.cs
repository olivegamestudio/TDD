using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The character being played: everything the player has earned, owns and has done, and the ship
/// they are currently in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The character persists and the ship does not.</b> That split is pillar 4 — the persistent
/// record survives, and death costs position only. Progression, credits, standing, possessions and
/// quest history are all things the player <em>has</em>; a hull is a thing they are currently
/// sitting in. Losing the ship must not be able to take the rest with it, so the rest does not live
/// on the ship.
/// </para>
/// <para>
/// Nothing here is written to the save game yet. The save carries position and quest state, and
/// widening it is a change to what older saves can be read back into — a decision of its own rather
/// than a side effect of the model existing.
/// </para>
/// </remarks>
public sealed class Character
{
    /// <summary>
    /// Creates a character at the start of their story, stocked with whatever the template says they
    /// begin with.
    /// </summary>
    /// <param name="template">The character content authored.</param>
    /// <param name="quests">
    /// The quest log their story is recorded in. It is passed in rather than made here because the
    /// session subscribes to it once and outlives any one character — a new game replaces the
    /// character, and a log replaced with it would take every subscriber's wiring with it.
    /// </param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    public Character(CharacterTemplate template, QuestLog quests)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(quests);

        Template = template;
        Quests = quests;
        Inventory = new Inventory(template.StartingInventory);
    }

    /// <summary>
    /// Gets the content this character was made from.
    /// </summary>
    public CharacterTemplate Template { get; }

    /// <summary>
    /// Gets how far the character has come.
    /// </summary>
    public Progression Progression { get; } = new();

    /// <summary>
    /// Gets how much money the character has.
    /// </summary>
    public int Credits { get; private set; }

    /// <summary>
    /// Gets what each group makes of the character.
    /// </summary>
    public Reputation Reputation { get; } = new();

    /// <summary>
    /// Gets what the character owns. A ship's loadout is fitted from here.
    /// </summary>
    public Inventory Inventory { get; }

    /// <summary>
    /// Gets the character's story so far.
    /// </summary>
    public QuestLog Quests { get; }

    /// <summary>
    /// Gets the ship the character is currently in, or <c>null</c> before they have been given one.
    /// </summary>
    /// <remarks>
    /// Nullable because a character exists before a hull is built for them, and because losing the
    /// hull has to be expressible without losing the character. It is not nullable for long: a
    /// session provisions a ship as part of starting or resuming a game.
    /// </remarks>
    public Ship? Ship { get; private set; }

    /// <summary>
    /// Puts the character into a ship.
    /// </summary>
    /// <param name="ship">The ship they are now flying.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ship"/> is <c>null</c>.</exception>
    public void Board(Ship ship)
    {
        ArgumentNullException.ThrowIfNull(ship);

        Ship = ship;
    }

    /// <summary>
    /// Adds to the character's money.
    /// </summary>
    /// <param name="credits">How much to add. Zero is allowed and does nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative. Earning a negative amount is spending, and the two need telling apart
    /// the moment anything reports what a job paid.
    /// </exception>
    public void Earn(int credits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(credits);

        Credits += credits;
    }

    /// <summary>
    /// Takes from the character's money.
    /// </summary>
    /// <param name="credits">How much to spend.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is negative, or more than the character has. Debt is not a state anything else
    /// knows how to read, so a purchase that cannot be afforded is refused where it is attempted.
    /// </exception>
    public void Spend(int credits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(credits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(credits, Credits);

        Credits -= credits;
    }
}
