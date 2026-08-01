namespace OliveGameStudio;

/// <summary>
/// A ship a player can be flying: what it is called in a save, what stands for it on screen, and
/// how it handles.
/// </summary>
/// <remarks>
/// <para>
/// The engine owns the concept of a ship and the physics that flies one; which ships exist is game
/// content, supplied through <see cref="IShipyard"/>. Nothing here knows what any particular ship
/// is called in the fiction.
/// </para>
/// <para>
/// A ship is a value rather than an entity. There is one player flying one ship at a time, and the
/// ship they are flying is a fact about the session rather than an object with a life of its own;
/// awarding a ship is therefore assigning a value, and two sessions awarded the same ship hold
/// equal values rather than a shared mutable object one could change under the other.
/// </para>
/// </remarks>
/// <param name="Id">
/// The identifier the ship is saved under. It is written into save games, so it must not change
/// once a build has shipped, and — like a quest id — it is an identifier rather than text and is
/// never translated.
/// </param>
/// <param name="AssetKey">
/// The key of the graphic that stands for this ship. The model says <em>which</em> asset
/// represents the ship, which is content the game declares; it says nothing about where, how big
/// or at what angle it is drawn, which stays entirely with whatever does the drawing.
/// </param>
/// <param name="Handling">How the ship flies. See <see cref="ShipHandling"/>.</param>
public sealed record Ship(string Id, string AssetKey, ShipHandling Handling);
