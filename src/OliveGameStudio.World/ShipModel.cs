namespace OliveGameStudio;

/// <summary>
/// A ship as a thing the player can own: which ship it is, what it looks like, and how it flies.
/// </summary>
/// <remarks>
/// <para>
/// The engine ships the shape; a game ships the instances. Nothing here knows what a ship costs,
/// what it is called in the fiction, or how it is come by — those are content decisions, and this
/// is only the record of which ship the player is in.
/// </para>
/// <para>
/// This is deliberately separate from <see cref="ShipHandling"/>. Handling is what the physics
/// needs and nothing else; a ship is also an identity that has to survive a save and an asset the
/// presentation has to draw. Folding the two together would put an asset key in front of
/// <see cref="ShipMovement"/>, which has no business knowing what the ship looks like.
/// </para>
/// </remarks>
/// <param name="Id">
/// The stable identifier for this ship, written to the save game. An identifier, not text: it is
/// never translated, because a save written in one language has to load in another.
/// </param>
/// <param name="AssetKey">
/// The key the ship's graphic is loaded under. Also an identifier, and also never translated.
/// The engine does not load it — whatever draws the ship does — but the ship is where the answer
/// to "what does this look like" lives, so a second ship brings its own graphic with it.
/// </param>
/// <param name="Handling">How the ship flies: its acceleration, drag and turn rate.</param>
public sealed record ShipModel(string Id, string AssetKey, ShipHandling Handling);
