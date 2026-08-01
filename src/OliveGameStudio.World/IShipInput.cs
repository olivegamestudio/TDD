namespace OliveGameStudio;

/// <summary>
/// Where the pilot's intent comes from. The physics reads this once a frame and never learns
/// whether a keyboard, a gamepad or a test produced the answer.
/// </summary>
/// <remarks>
/// The platform host owns the real input device, so this is the seam it binds to — the same split
/// the engine already uses for saved progress: the engine provides the service, the host provides
/// the device.
/// </remarks>
public interface IShipInput
{
    /// <summary>
    /// Reads what the pilot is asking for this frame.
    /// </summary>
    /// <returns>
    /// The controls as they stand now. Called once per frame on the frame path, so an
    /// implementation should sample the device rather than wait on it.
    /// </returns>
    ShipControls Read();
}
