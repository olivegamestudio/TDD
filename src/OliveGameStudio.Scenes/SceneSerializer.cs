using System.Text.Json;
using System.Text.Json.Serialization;

namespace OliveGameStudio;

/// <summary>
/// Turns a <see cref="SceneDefinition"/> into JSON and back. This is the contract between the game,
/// which streams regions, and the editor, which writes them — so both sides read the same file
/// through the same code rather than agreeing by hand and drifting apart.
/// </summary>
/// <remarks>
/// <para>
/// Reading is deliberately forgiving, for the same reason reading a save is: a region that cannot
/// be understood should leave the player somewhere empty rather than stop the game. A missing,
/// blank or damaged file comes back as <see cref="SceneDefinition.Empty"/>, and an entry the build
/// does not understand is skipped rather than taking the region down with it.
/// </para>
/// <para>
/// <b>It is not forgiving about being asked to write nonsense.</b> Serialising is the editor's
/// side of the contract, and a file written wrong is a file every later read has to cope with — so
/// failures there are raised where they happen.
/// </para>
/// </remarks>
public static class SceneSerializer
{
    /// <summary>
    /// How a region is written: indented, so a diff of an authored file is readable by whoever has
    /// to review it, and named in camel case because that is the convention the rest of this
    /// project's JSON already follows.
    /// </summary>
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Writes a region as JSON.
    /// </summary>
    /// <param name="scene">The region to write.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scene"/> is <c>null</c>.</exception>
    public static string Serialize(SceneDefinition scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return JsonSerializer.Serialize(scene, Options);
    }

    /// <summary>
    /// Reads a region from JSON, or hands back <see cref="SceneDefinition.Empty"/> if the text is
    /// missing, blank or not a region this build can read.
    /// </summary>
    /// <param name="json">The text to read.</param>
    /// <returns>The region, or an empty one.</returns>
    public static SceneDefinition Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SceneDefinition.Empty;
        }

        try
        {
            // Null rather than an exception is what a syntactically valid "null" deserialises to,
            // and it means the same thing here as a file that could not be parsed at all.
            return JsonSerializer.Deserialize<SceneDefinition>(json, Options) ?? SceneDefinition.Empty;
        }
        catch (JsonException)
        {
            // Deliberately only JsonException: that is the file being wrong, which is the case this
            // is forgiving about. Anything else is this code being wrong, and burying that would
            // turn a defect into a silently empty world.
            return SceneDefinition.Empty;
        }
    }
}
