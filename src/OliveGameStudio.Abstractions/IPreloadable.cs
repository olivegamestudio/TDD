namespace OliveGameStudio;

/// <summary>
/// Represents a component whose assets can be loaded ahead of time and turned
/// into runtime resources, allowing expensive work to be performed during a
/// loading phase rather than on first use.
/// </summary>
public interface IPreloadable
{
    /// <summary>
    /// Asynchronously loads the raw asset data required by this component,
    /// typically from disk or another I/O source, so it is ready for
    /// <see cref="CreateResources"/> to consume.
    /// </summary>
    /// <param name="cancellation">Token used to cancel the load operation.</param>
    /// <returns>A task that completes once the asset data has been loaded.</returns>
    Task LoadAsync(CancellationToken cancellation);

    /// <summary>
    /// Creates runtime resources from the previously loaded asset data, doing at
    /// most the amount of work permitted by <paramref name="budget"/> so the cost
    /// can be spread across multiple calls (for example, one per frame).
    /// </summary>
    /// <param name="budget">The maximum amount of work to perform in this call.</param>
    /// <returns>
    /// <c>true</c> when all resources have been created and no further calls are
    /// required; otherwise <c>false</c> to indicate more work remains.
    /// </returns>
    bool CreateResources(int budget);
}