namespace OliveGameStudio;

/// <summary>
/// Filters the real elapsed frame time before it reaches the game, decoupling the rate at
/// which the game advances from the rate at which the platform produces frames.
/// Implementations decide the policy: scaling for pause and slow motion, clamping to absorb
/// stalls, or quantising to a fixed timestep.
/// </summary>
public interface IFrameTimeController
{
    /// <summary>
    /// Filters a frame time according to the implementation's policy.
    /// </summary>
    /// <param name="frameTime">The real time that has elapsed since the last frame.</param>
    /// <returns>The frame time that the game should actually advance by.</returns>
    TimeSpan Filter(TimeSpan frameTime);
}
