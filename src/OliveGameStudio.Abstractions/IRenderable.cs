namespace OliveGameStudio;

/// <summary>
/// Something that can put itself on screen. Screens implement this in addition to
/// <see cref="IScreen"/> when they have anything to draw; a screen that draws nothing simply
/// does not implement it, and the screen director skips it.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="IScreen"/> rather than a second method on it. Update
/// and draw run at different times for different reasons — a paused game still draws — and
/// keeping them apart is what lets the logic stages land tested and headless while the drawing
/// arrives afterwards.
/// </remarks>
public interface IRenderable
{
    /// <summary>
    /// Draws this object into <paramref name="renderer"/>. Called at most once per frame, after
    /// the frame's updates, and must not change game state: a frame the platform chooses to
    /// skip drawing has to leave the game in the same place it would otherwise be.
    /// </summary>
    /// <param name="renderer">The frame's renderer.</param>
    void Render(IRenderer renderer);
}
