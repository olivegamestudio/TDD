using System.Text.Json;
using System.Text.Json.Serialization;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Turns a <see cref="SaveGame"/> into the text handed to the save progress service, and back.
/// Reading is deliberately forgiving: a missing or damaged save is reported as "no save" rather
/// than as an error, so a player with a corrupt file gets a new game instead of a crash.
/// </summary>
/// <remarks>
/// What this type refuses is a judgement about what this build will take, not a measurement of
/// what is recoverable, so <see cref="GameSession.Continue"/> sets a refused save aside before the
/// new game writes. A boundary drawn here that turns out to be too strict costs the player a
/// restart rather than their game.
/// </remarks>
public static class SaveGameSerializer
{
    static readonly JsonSerializerOptions Options = new()
    {
        // Quest states by name, so reordering the enum cannot silently change a saved state — and
        // by name only. Left to itself the converter also reads numbers, and reads them without
        // checking they name a state at all, so a save saying 99 would load as a quest stuck in a
        // state nothing can move it out of. A number is not a state this build wrote.
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
        WriteIndented = true,
    };

    /// <summary>
    /// Writes a snapshot as text.
    /// </summary>
    /// <param name="save">The snapshot to write.</param>
    /// <returns>The serialised save.</returns>
    public static string Serialize(SaveGame save) => JsonSerializer.Serialize(save, Options);

    /// <summary>
    /// Reads a snapshot back from text.
    /// </summary>
    /// <param name="content">The serialised save, or <c>null</c> when there is no save.</param>
    /// <returns>
    /// The snapshot, or <c>null</c> when <paramref name="content"/> is missing, blank, or not a
    /// save this build can read.
    /// </returns>
    public static SaveGame? Deserialize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            // A save written as JSON null is no save. One with a null quest list is still
            // readable, and reads back as a save with no quests, because SaveGame.Quests refuses
            // a null list where it is set rather than leaving each reader to cope with one.
            return JsonSerializer.Deserialize<SaveGame>(content, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
