namespace BattleForce2249;

/// <summary>
/// States the display the game is built to fill. This is the one place in the repository that
/// says how wide a screen the game supports; everything that needs to hold something against a
/// screen derives its number from here rather than stating one of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> The game never sets a window size — the viewport is whatever
/// the device reports, and <see cref="OliveGameStudio.ICamera.PixelsPerUnit"/> is left at one — so
/// "the screens this game supports" was not written down anywhere. Anything that needed it had to
/// guess, and a guess buried in a rendering constant is a product decision nobody made. Putting it
/// here makes it a decision, keeps it in the same place as the game's other stated facts, and lets
/// a build that targets a different display say so in configuration rather than in code.
/// </para>
/// <para>
/// <b>It does not resize anything.</b> Nothing reads this to pick a resolution or to letterbox;
/// the window is still whatever the platform gives. It is the screen that validation is taken
/// against — the bar a piece of content has to clear before it can claim to fill the screen.
/// </para>
/// </remarks>
public sealed class DisplayOptions
{
    /// <summary>
    /// The name of the configuration section these options bind from.
    /// </summary>
    public const string SectionName = "Display";

    /// <summary>
    /// The width the game supports unless configuration says otherwise: 7680 pixels, which covers
    /// 8K and every ultrawide sold today.
    /// </summary>
    /// <remarks>
    /// The safe direction to be wrong in is upwards. Overstating the supported display only makes
    /// the checks taken against it stricter, and the strictest of them — the floor under a star
    /// layer's tile size — still leaves the shipping layers an order of magnitude of headroom.
    /// Understating it lets content through that leaves a blank border on any display wider than
    /// the guess, which is a defect the player sees. A narrower default would already have been
    /// wrong for the 5120-pixel ultrawides that exist.
    /// </remarks>
    public const float DefaultWidestSupportedViewportInPixels = 7680f;

    /// <summary>
    /// The widest viewport the game is required to fill, in pixels at one pixel per unit.
    /// </summary>
    /// <remarks>
    /// Measured along one axis and applied to both, because content is held against the longest
    /// span it could be asked to cover and a display is not required to be square.
    /// </remarks>
    public float WidestSupportedViewportInPixels { get; set; } = DefaultWidestSupportedViewportInPixels;
}
