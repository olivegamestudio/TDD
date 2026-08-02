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
/// <para>
/// What this type refuses is a judgement about what this build will take, not a measurement of
/// what is recoverable, so <see cref="GameSession.Continue"/> sets a refused save aside before the
/// new game writes. A boundary drawn here that turns out to be too strict costs the player a
/// restart rather than their game.
/// </para>
/// <para>
/// <b>A quest entry that names no registered quest is drift, not damage.</b> It costs the entry
/// and nothing else: the file is read, and <see cref="QuestLog.Restore"/> skips the entry, exactly
/// as it skips one naming a quest this build no longer ships. A blank identifier and a dropped one
/// mean the same thing to everything downstream — there is no quest to apply them to — so refusing
/// the file over one would discard the progress saved beside it to protect against an entry that
/// does nothing. What refusal is for is a file this build cannot read: a state outside a quest's
/// lifecycle, or JSON that will not parse.
/// </para>
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
            SaveGame? save = JsonSerializer.Deserialize<SaveGame>(content, Options);

            // a save written as JSON null, or with a null quest list, is still readable
            return save is null ? null : save with { Quests = save.Quests ?? [] };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
