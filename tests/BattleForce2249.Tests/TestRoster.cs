using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A roster with one plain character, so a session test can start a game without depending on the
/// game's own content.
/// </summary>
/// <remarks>
/// The name is a literal rather than a look-up on purpose: these tests are about sessions, and a
/// session test that reads the language files fails for a reason that has nothing to do with what
/// it is testing. <c>BattleForceRosterTests</c> is where the shipped character is checked.
/// </remarks>
sealed class TestRoster : ICharacterRoster
{
    /// <summary>
    /// The identifier of the character these tests play as.
    /// </summary>
    public const string TemplateId = "test-character";

    /// <summary>
    /// The place these tests start at. The test worlds name no places and put everything at their
    /// own start, so what this says matters only in the test that checks it is asked for.
    /// </summary>
    public const string StartLocation = "test-location";

    /// <summary>
    /// The hull the test character flies: the shipped handling, so the physics under a session test
    /// behaves the way the game's does, with round numbers for the pools nothing empties yet.
    /// </summary>
    public static ShipProfile Profile { get; } = new(
        DisgracedShip.Handling,
        Health: 100,
        Durability: 100,
        CargoSlots: 16,
        Loadout: []);

    /// <inheritdoc />
    public IReadOnlyList<CharacterTemplate> Templates => [Starting];

    /// <inheritdoc />
    public CharacterTemplate Starting { get; } =
        new(TemplateId, "A test character", Profile, StartLocation, []);
}
