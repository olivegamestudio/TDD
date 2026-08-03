namespace BattleForce2249;

/// <summary>
/// The seam a game supplies its characters through, so a session can begin a game without learning
/// which characters this game has.
/// </summary>
/// <remarks>
/// The same shape <see cref="Pilgrimage.ICampaign"/> uses for quests and <see cref="IWorld"/> uses
/// for places: the engine side asks, the game side answers with content.
/// </remarks>
public interface ICharacterRoster
{
    /// <summary>
    /// Gets every character the game ships.
    /// </summary>
    IReadOnlyList<CharacterTemplate> Templates { get; }

    /// <summary>
    /// Gets the character a new game begins as.
    /// </summary>
    /// <remarks>
    /// A separate member rather than "the first one in the list", because which character a new game
    /// starts as is a decision content makes and an ordering is not a decision. There is no
    /// character selection yet; when there is, this is what it overrides.
    /// </remarks>
    CharacterTemplate Starting { get; }
}

/// <summary>
/// The characters Battle Force 2249 ships with.
/// </summary>
public sealed class BattleForceRoster : ICharacterRoster
{
    /// <summary>
    /// The identifier the Disgraced is saved under. Save games refer to it, so it must not change,
    /// and unlike the name it is never translated.
    /// </summary>
    public const string DisgracedId = "disgraced";

    /// <inheritdoc />
    /// <remarks>
    /// Built on each read rather than once, because the names are translated: the roster is a
    /// singleton, so a list built in a field initialiser would freeze the player's language at
    /// startup. Read only when a game starts or resumes, so building it is not on the frame path.
    /// This is the same reasoning <see cref="BattleForceCampaign.Quests"/> is built on.
    /// </remarks>
    public IReadOnlyList<CharacterTemplate> Templates => [Disgraced];

    /// <inheritdoc />
    /// <remarks>
    /// Named rather than taken from the front of <see cref="Templates"/>. There is one character to
    /// choose from today, so the two cannot differ — which is exactly when a list's ordering
    /// quietly becomes a decision nobody wrote down.
    /// </remarks>
    public CharacterTemplate Starting => Disgraced;

    /// <summary>
    /// "You are <b>the Disgraced</b>" — sole survivor of an erased bloodline, no faction, critically
    /// low on everything, and the only character this build ships.
    /// </summary>
    static CharacterTemplate Disgraced =>
        new(
            DisgracedId,
            // in the player's language; the id above stays the same in every language
            GameText.DisgracedName,
            DisgracedShip.Profile,
            // the game opens inside the collapsing mines, which is where quest 1 begins
            BattleForceWorld.Mines,
            // "critically low on everything": the Disgraced starts with the hull and nothing else
            StartingInventory: []);
}
