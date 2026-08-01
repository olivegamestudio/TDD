namespace OliveGameStudio;

/// <summary>
/// Represents the entry point of a game, decoupling the game's flow from the platform
/// that runs it. A platform host (for example the MonoGame desktop host) owns the window
/// and the frame loop, and drives the game purely through this interface, which keeps the
/// game logic testable without a rendering framework.
/// </summary>
public interface IHost
{
    /// <summary>
    /// Performs one-time initialisation of the game, such as wiring up screen transitions
    /// and navigating to the initial screen. Called once by the platform host before the
    /// first call to <see cref="Update"/>.
    /// </summary>
    void Start();

    /// <summary>
    /// Advances the game state by one frame. Called repeatedly by the platform host for
    /// the lifetime of the game, after <see cref="Start"/>.
    /// </summary>
    /// <param name="frameTime">
    /// The time that has elapsed since the last frame. This value is used to calculate
    /// updates in game logic, animations, and other time-dependent processes.
    /// </param>
    void Update(TimeSpan frameTime);

    /// <summary>
    /// Draws the current state of the game. Called by the platform host after
    /// <see cref="Update"/>, and separately from it: the platform decides how often a frame is
    /// drawn, and a frame it chooses to skip must leave the game where it would otherwise be.
    /// </summary>
    /// <param name="renderer">The frame's renderer, owned by the platform host.</param>
    void Draw(IRenderer renderer);
}