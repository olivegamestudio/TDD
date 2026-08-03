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
    /// <remarks>
    /// <para>
    /// <b>Refusal is per file; drift is per entry.</b> The two shapes look alike and are not the
    /// same. A quest entry that <em>names no quest</em> — a <see cref="QuestProgress.QuestId"/>
    /// that is absent, <c>null</c> or blank — names nothing this build ships, which is exactly how
    /// <see cref="QuestLog.Restore"/> reads it: there is nothing to apply it to, so the entry is
    /// dropped and the rest of the file is read. A quest entry that <em>is not there</em> is not
    /// drift; no build wrote a <c>null</c> into the list, so the file is refused whole.
    /// </para>
    /// <para>
    /// Both edges give that same answer, and the agreement is the point. While they disagreed, one
    /// blank line in a file discarded the completed campaign saved beside it — and discarded it for
    /// good, because a refused save is set aside and played over. Every refusal here is final, so
    /// the narrowing is deliberate rather than a convenience.
    /// </para>
    /// <para>
    /// <b>A position is refused whole, like a state this build did not write.</b> A file whose
    /// coordinates are past what can be drawn is reported as "no save" rather than being clamped
    /// to somewhere the player never was — see <see cref="SaveGame.CanBeResumed"/> for where that
    /// edge is. Clamping would silently move a player who cannot be told they were moved; this at
    /// least starts a game that works.
    /// </para>
    /// </remarks>
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

            if (save.Quests.Any(quest => quest is null))
            {
                return null;
            }

            // A position the game cannot be resumed at is not a save this build can read, for the
            // same reason a quest state it never wrote is not. See SaveGame.CanBeResumed for where
            // the edge is and why it is there.
            if (!save.CanBeResumed)
            {
                return null;
            }

            // Dropped here rather than carried on: an entry naming no quest matches nothing the
            // campaign registered, so nothing downstream could act on it, and the rest of the load
            // never has to hold an entry it must remember to ignore.
            return save with { Quests = [.. save.Quests.Where(NamesAQuest)] };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a quest entry names a quest at all. A missing, <c>null</c> or blank identifier is
    /// not a name: no quest can be registered under one — <see cref="QuestLog.Register"/> refuses
    /// it — and no save this build wrote holds one.
    /// </summary>
    /// <remarks>
    /// The first half of that used to be an assumption. Nothing stopped a campaign registering a
    /// quest under a blank identifier, and one that did would have its progress captured and then
    /// dropped here, so the entry this method skips as meaningless was the only record of a quest
    /// the player had really finished. <see cref="QuestLog.Register"/> now closes that door, which
    /// is why this can state it. Both edges test the identifier the same way, and that agreement is
    /// pinned by tests rather than left to the two of them to keep separately.
    /// </remarks>
    /// <param name="quest">The parsed quest entry.</param>
    /// <returns><c>true</c> when the entry names something.</returns>
    static bool NamesAQuest(QuestProgress quest) => !string.IsNullOrWhiteSpace(quest.QuestId);
}
