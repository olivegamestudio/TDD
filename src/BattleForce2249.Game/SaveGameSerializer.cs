using System.Text.Json;
using System.Text.Json.Serialization;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Turns a <see cref="SaveGame"/> into the text handed to the save progress service, and back.
/// Reading is deliberately forgiving: a missing or damaged save is reported as "no save" rather
/// than as an error, so a player with a corrupt file gets a new game instead of a crash.
/// </summary>
public static class SaveGameSerializer
{
    static readonly JsonSerializerOptions Options = new()
    {
        // quest states by name, so reordering the enum cannot silently change a saved state
        Converters = { new JsonStringEnumConverter() },
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
            SaveGame? save = JsonSerializer.Deserialize<SaveGame>(content, Options);

            // a save written as JSON null, or with a null quest list or ship, is still readable
            return save is null
                ? null
                : save with
                {
                    ShipId = save.ShipId ?? string.Empty,
                    Quests = save.Quests ?? [],
                };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
