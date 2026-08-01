namespace OliveGameStudio;

/// <summary>
/// A ship a player can be flying: what it is called in a save, what represents it on screen, and
/// how it handles.
/// </summary>
/// <remarks>
/// <para>
/// Data rather than a hierarchy. The engine owns one physics and every ship is another instance of
/// this, so a second hull is a second set of numbers rather than a second type — which is what
/// makes a ship something a save game can name and hand back.
/// </para>
/// <para>
/// The engine ships no ships. This says what a ship <em>is</em>; which ships exist is content, and
/// a game supplies them.
/// </para>
/// <para>
/// Both strings are identifiers, not text. <see cref="Id"/> is written into save games and
/// <see cref="AssetKey"/> names an asset; translating either would mean a game saved in one
/// language could not be loaded in another, and a ship that draws in English would draw as nothing
/// in German. A ship's <em>name</em>, if the player is ever shown one, is a separate translated
/// string and does not belong here.
/// </para>
/// </remarks>
/// <param name="Id">
/// The identifier the ship is saved under. It must be stable across builds, because a save written
/// by one build names its ship to another.
/// </param>
/// <param name="AssetKey">
/// The key of the asset that represents the ship. The model only says which asset; loading and
/// drawing it is the presentation's job, and reading the key from here rather than hard-coding it
/// at the draw site is what makes the picture follow the ship the player was awarded.
/// </param>
/// <param name="Handling">How the ship flies.</param>
public sealed record Ship(string Id, string AssetKey, ShipHandling Handling);
