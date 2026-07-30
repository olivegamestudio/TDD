namespace OliveGameStudio;

/// <summary>
/// An <see cref="IFrameTimeController"/> that hands the real frame time straight to the game.
/// This is the shipping default: the game advances at exactly the rate the platform produces
/// frames, with no scaling, pausing, or smoothing in the way.
/// </summary>
public sealed class PassThroughFrameTimeController : IFrameTimeController
{
    /// <summary>
    /// Returns the frame time unchanged.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>The same value as <paramref name="frameTime"/>.</returns>
    public TimeSpan Filter(TimeSpan frameTime) => frameTime;
}
