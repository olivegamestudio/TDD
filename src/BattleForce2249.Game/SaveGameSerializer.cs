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
            SaveGame? save = JsonSerializer.Deserialize<SaveGame>(content, Options);
            if (save is null)
            {
                return null;
            }

            // a save written as JSON null, or with a null quest list, is still readable
            save = save with { Quests = save.Quests ?? [] };

            return IsPlayable(save) ? save : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a save that parsed is one the game can actually be resumed from.
    /// </summary>
    /// <remarks>
    /// Parsing is not the whole of "a save this build can read". Content that is well-formed JSON
    /// but not a game — a quest entry that is not there, one naming no quest, a coordinate that is
    /// not a place — used to be handed on as valid and then throw or brick further in, where the
    /// player's only symptom was a game that quietly stopped. This is the one point that decides,
    /// so everything downstream can be written against a save that makes sense, and the answer for
    /// anything that does not is the one the class already promises: no save, so a new game.
    /// </remarks>
    /// <param name="save">The parsed save.</param>
    /// <returns><c>true</c> when the save can be resumed from.</returns>
    static bool IsPlayable(SaveGame save) =>
        // Infinity is the obvious one, and it does not come from a broken file: 1e400 is valid JSON
        // that overflows on the way in. But finiteness was never the condition — 1e300 is finite,
        // one character away in the file, and lands the player somewhere no flight can carry them
        // back from, which is the same quest that can never start and never complete. The world
        // states how far it goes; a save outside it did not come from playing this game.
        IsInTheWorld(save.PlayerX)
        && IsInTheWorld(save.PlayerY)
        // A quest entry with no id is not this build's writing, and it is what QuestLog refuses.
        && save.Quests.All(quest => quest is not null && !string.IsNullOrWhiteSpace(quest.QuestId));

    /// <summary>
    /// Whether a saved coordinate names a place inside the world. NaN answers <c>false</c> from
    /// both comparisons, so it is refused without being named separately.
    /// </summary>
    /// <param name="coordinate">The coordinate read from the save.</param>
    /// <returns><c>true</c> when the coordinate is somewhere the game could have put the player.</returns>
    static bool IsInTheWorld(double coordinate) =>
        coordinate >= -BattleForceWorld.Extent && coordinate <= BattleForceWorld.Extent;
}
