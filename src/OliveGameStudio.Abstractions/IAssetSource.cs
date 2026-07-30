namespace OliveGameStudio;

/// <summary>
/// Provides access to raw asset data, abstracting over where and how the bytes
/// for a given asset key are stored (for example, on disk, in an archive, or
/// embedded in the application).
/// </summary>
public interface IAssetSource
{
    /// <summary>
    /// Asynchronously reads the raw bytes for each of the requested asset keys.
    /// </summary>
    /// <param name="keys">The asset keys to read.</param>
    /// <param name="cancellation">Token used to cancel the read operation.</param>
    /// <returns>
    /// A task that completes with a list pairing each key with its loaded bytes.
    /// </returns>
    Task<IReadOnlyList<(string Key, byte[] Bytes)>> ReadAllAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellation);
}