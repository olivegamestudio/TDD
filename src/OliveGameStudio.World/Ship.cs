namespace OliveGameStudio;

/// <summary>
/// A ship a player can own and fly: what it is called in a save, how it handles, and which asset
/// draws it.
/// </summary>
/// <remarks>
/// <para>
/// The engine provides the type; a game provides the instances. This holds no numbers of its own —
/// <see cref="ShipHandling"/> already owns those and <see cref="ShipMovement"/> already owns the
/// physics they are flown through. What it adds is ownership: a ship is something the player is
/// given, keeps, and is still flying after a reload, which neither a handling profile nor a
/// movement integrator can express on its own.
/// </para>
/// <para>
/// <see cref="AssetKey"/> lives on the model rather than in whatever draws it, so a ship arrives
/// already knowing which picture stands for it and the renderer is left with nothing to decide.
/// It is an identifier, never text, and so is never translated — a save written in one language
/// has to load in another.
/// </para>
/// </remarks>
/// <param name="Id">
/// The identifier the ship is saved under. It must not change once a build has shipped, because
/// saves refer to it.
/// </param>
/// <param name="Handling">How the ship flies.</param>
/// <param name="AssetKey">The key of the asset that draws the ship.</param>
public sealed record Ship(string Id, ShipHandling Handling, string AssetKey);
