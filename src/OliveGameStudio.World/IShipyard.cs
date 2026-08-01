namespace OliveGameStudio;

/// <summary>
/// The seam a game supplies its ships through: every ship it ships with, which one a new game
/// awards, and how a ship named in a save is found again.
/// </summary>
/// <remarks>
/// The engine provides the service — a ship exists, it has handling, it can be flown — and the game
/// provides the content. This is the same shape the campaign uses for quests, for the same reason:
/// the session has to award and restore a ship without ever learning what ships this game has.
/// </remarks>
public interface IShipyard
{
    /// <summary>
    /// Gets every ship the game ships with.
    /// </summary>
    IReadOnlyList<Ship> Ships { get; }

    /// <summary>
    /// Gets the ship a brand new game awards the player.
    /// </summary>
    Ship StartingShip { get; }

    /// <summary>
    /// Finds a ship by the identifier it was saved under.
    /// </summary>
    /// <param name="id">The identifier to look for, which may be missing from an older save.</param>
    /// <returns>
    /// The ship, or <c>null</c> when the identifier is missing, blank, or names a ship this build
    /// no longer has. Reporting rather than throwing is deliberate: a save that names a ship the
    /// game has since dropped has to load into something, and losing the ship is a better outcome
    /// than losing the save.
    /// </returns>
    Ship? Find(string? id);
}
