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
/// <para>
/// <b>The screen is declared at both ends</b>, because content can be wrong in both directions:
/// something sown too finely to cover the widest display leaves a border on it, and something sown
/// too coarsely to register on the narrowest leaves that one empty. A bound in either direction is
/// an ordered comparison against a screen, so both screens are stated here and neither is a
/// constant inside the thing measuring itself.
/// </para>
/// </remarks>
public sealed class DisplayOptions
{
    /// <summary>
    /// The name of the configuration section these options bind from.
    /// </summary>
    public const string SectionName = "Display";

    /// <summary>
    /// The declared widest screen a build gets when configuration says nothing — 7680 pixels, or
    /// 8K.
    /// </summary>
    /// <remarks>
    /// Named rather than inlined into the property so that a doc comment, a test or a message can
    /// point at the default rather than restate it. A restated number is the drift #40 and #50 were
    /// both raised for.
    /// </remarks>
    public const float DefaultWidestSupportedViewportInPixels = 7680f;

    /// <summary>
    /// The declared narrowest screen a build gets when configuration says nothing — 720 pixels,
    /// the short side of a 720p display.
    /// </summary>
    public const float DefaultNarrowestSupportedViewportInPixels = 720f;

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
    public float WidestSupportedViewportInPixels { get; set; } =
        DefaultWidestSupportedViewportInPixels;

    /// <summary>
    /// The narrowest <em>side</em> of the smallest screen the game promises to look right on, in
    /// pixels. Defaults to 720 — the short side of a 720p display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The narrowest side rather than a width</b>, because what holds itself against this is
    /// square — a star field's tile — and a square has to fit the shorter axis. A number taken from
    /// the long side would pass content that a portrait or a windowed player never sees.
    /// </para>
    /// <para>
    /// <b>The safe direction to be wrong in is the opposite one.</b> Where the widest screen is
    /// only ever used to raise a floor, this is only ever used to lower a ceiling, so guessing too
    /// small costs a little coarseness at the top end and guessing too large admits content that
    /// shows a smaller display nothing at all. That asymmetry is why the two defaults are not
    /// chosen the same way.
    /// </para>
    /// <para>
    /// It must be at most <see cref="WidestSupportedViewportInPixels"/>. A narrowest screen wider
    /// than the widest one is not a tight bound, it is a contradiction — the floor it raises would
    /// sit above the ceiling it lowers, and every layer put to the pair would be refused with
    /// advice pointing in both directions at once.
    /// </para>
    /// </remarks>
    public float NarrowestSupportedViewportInPixels { get; set; } =
        DefaultNarrowestSupportedViewportInPixels;
}
