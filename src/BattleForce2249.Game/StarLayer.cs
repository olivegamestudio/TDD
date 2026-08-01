namespace BattleForce2249;

/// <summary>
/// One depth of stars in a <see cref="StarField"/>: how far it lags the camera, how thickly it is
/// sown, and how big its stars are drawn.
/// </summary>
/// <remarks>
/// <para>
/// A layer describes a field of unbounded extent, not a list of stars. Where its stars stand is
/// derived from the layer and the tile they fall in, so the field is the same field however far
/// the player flies and whichever direction they come back from.
/// </para>
/// <para>
/// Every number here is a finite one, and <see cref="StarField.Layers"/> refuses a layer that
/// carries anything else. A star's position is derived from these by arithmetic, so a single
/// <c>NaN</c> or infinity anywhere in a layer spreads to every star it sows and puts the whole
/// field off the screen.
/// </para>
/// </remarks>
/// <param name="Parallax">
/// How much of the camera's movement this layer takes, from just above zero to one. One is fixed
/// in the world — it slides past at the full speed the ship is flying — and smaller values lag
/// behind, which is what reads as distance. Zero is not allowed: a layer that never moves is
/// painted on the inside of the canopy and tells the player nothing about their speed.
/// </param>
/// <param name="TileSizeInWorldUnits">
/// The side of the square the layer is sown in. Smaller tiles mean more stars over the same
/// ground, and they are what bounds the work per frame: only the tiles the viewport covers are
/// ever drawn, so flying further costs nothing.
/// <para>
/// There is a floor on how small it can usefully be — see
/// <see cref="StarField.SmallestUsableTileSize"/> — because a frame visits at most
/// <see cref="StarField.MaxTilesPerAxis"/> tiles per axis. Below that the field is clipped to a
/// band around the camera and leaves a blank border, which is why <see cref="StarField.Layers"/>
/// refuses it rather than sowing a field that cannot cover the screen.
/// </para>
/// </param>
/// <param name="StarsPerTile">How many stars stand in each tile.</param>
/// <param name="SizeInPixels">
/// How big each star is drawn, in pixels rather than world units. A star is a point of light at
/// an unreachable distance; zooming in on the world should not make it a disc.
/// </param>
public readonly record struct StarLayer(
    float Parallax,
    float TileSizeInWorldUnits,
    int StarsPerTile,
    float SizeInPixels);
