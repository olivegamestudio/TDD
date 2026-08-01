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
/// "Damaged" is decided here by whether the game can resume, not by whether a parser was happy.
/// A snapshot that reads back perfectly and still cannot be played — see
/// <see cref="SaveGame.CanBeResumed"/> — is reported the same way as one that would not parse,
/// because the caller's fallback is the same and the alternative is a game that starts and then
/// fails somewhere further in, where nothing is left that can do anything about it.
/// </remarks>
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
    /// The snapshot, or <c>null</c> when <paramref name="content"/> is missing, blank, not a save
    /// this build can read, or a save this build could not resume from.
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
            if (save is null)
            {
                // a save written as JSON null
                return null;
            }

            // and one that parsed but leaves the player somewhere the game cannot draw from,
            // which no parser can see and every frame afterwards would trip over
            return save.CanBeResumed ? save with { Quests = save.Quests ?? [] } : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
