using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Reads an authored region from the files shipped beside the game.
/// </summary>
/// <remarks>
/// <para>
/// Regions are content, not code: they ship as files so a marker can be corrected or a new place
/// dropped in without a rebuild, exactly as the translations do.
/// </para>
/// <para>
/// A region that is not there, or cannot be read, comes back empty rather than throwing. That is
/// the same answer a damaged save gets and for the same reason — the player should end up
/// somewhere empty rather than somewhere stopped, and an empty region is visible immediately while
/// a crash on entering the world tells them nothing they can act on.
/// </para>
/// </remarks>
public sealed class RegionLoader
{
    /// <summary>
    /// The folder regions are read from, relative to the game.
    /// </summary>
    public const string FolderName = "Regions";

    readonly string _folder;

    /// <summary>
    /// Creates a loader reading from the regions shipped beside the running game.
    /// </summary>
    public RegionLoader()
        : this(Path.Combine(AppContext.BaseDirectory, FolderName))
    {
    }

    /// <summary>
    /// Creates a loader reading from a given folder, so a test can supply its own content without
    /// writing into the build output.
    /// </summary>
    /// <param name="folder">The folder to read regions from.</param>
    public RegionLoader(string folder) => _folder = folder;

    /// <summary>
    /// Reads the region with the given id.
    /// </summary>
    /// <param name="id">
    /// The region's identity, which is also its file name. An identifier, never translated.
    /// </param>
    /// <returns>The region, or <see cref="SceneDefinition.Empty"/> if it could not be read.</returns>
    public SceneDefinition Load(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            return SceneSerializer.Deserialize(File.ReadAllText(Path.Combine(_folder, $"{id}.json")));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The storage getting in the way rather than a defect — the same two this game already
            // treats that way when reading a save. Catching wider would bury real bugs behind an
            // empty world.
            return SceneDefinition.Empty;
        }
    }
}
