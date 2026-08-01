namespace BattleForce2249;

/// <summary>
/// What the game says about the screen it is drawn on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a statement, not a window size.</b> The game never asks for a resolution — the
/// viewport is whatever the device reports, and the drawing sizes itself to it every frame. What
/// this declares is the screen the game promises to look right on, which is a different thing and
/// a product decision rather than a rendering one. It exists because anything that has to size
/// itself against a display has to hold itself against *some* number, and a number invented at the
/// point of use is a guess that nobody else can find, agree with, or change.
/// </para>
/// <para>
/// The star field is the first thing to need it — see <c>StarField.SmallestUsableTileSize</c>,
/// which refuses a layer sown too finely to cover the screen — and it will not be the last, so the
/// number lives here rather than there.
/// </para>
/// </remarks>
public sealed class DisplayOptions
{
    /// <summary>
    /// The name of the configuration section these options bind from.
    /// </summary>
    public const string SectionName = "Display";

    /// <summary>
    /// The widest screen the game promises to fill, in pixels. Defaults to 7680 — 8K, and wider
    /// than any ultrawide sold today.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the default is set this high.</b> The number is only ever used to raise a floor
    /// under something that must cover the screen, so being wrong upwards costs a little detail
    /// and being wrong downwards leaves a visible gap on a display nobody thought about. 5120-pixel
    /// ultrawides already exist, so 3840 would have been wrong the day it was written; 7680 covers
    /// 8K and leaves room to be wrong for a while yet.
    /// </para>
    /// <para>
    /// <b>What raising it costs.</b> The floor rises with it, so a build that declares a wider
    /// screen than the content was sown for will start refusing content that used to be accepted.
    /// That is the point — it fails where the number was changed, rather than on the display
    /// nobody owns — but it means raising this is a decision to re-check the content against, not
    /// a free safety margin. The star layers the game ships cover a little over 22,000 pixels.
    /// </para>
    /// </remarks>
    public float WidestSupportedViewportInPixels { get; set; } = 7680f;
}
