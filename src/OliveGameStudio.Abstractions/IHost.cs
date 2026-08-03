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
    /// Hands the game one frame of input from the platform host's devices.
    /// </summary>
    /// <param name="frame">Every device as the platform host read it this frame.</param>
    /// <remarks>
    /// Input is pushed rather than pulled, and that is what makes the device the player is on a
    /// thing the game can know. A game that reached out for each device when it happened to need
    /// one would read the keyboard in one place for the menu and in another for the ship, with no
    /// single point that could say which of them the player is actually using — and choosing
    /// between devices is exactly the decision this entry exists to allow.
    /// <para>
    /// Called once a frame, before <see cref="Update"/>, so a frame acts on the input that arrived
    /// with it. A platform host that skips it leaves the game with the previous frame's input,
    /// which is the same as a player holding still.
    /// </para>
    /// </remarks>
    void Input(InputFrame frame);

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