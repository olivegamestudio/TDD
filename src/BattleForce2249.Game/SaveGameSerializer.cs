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
/// "Damaged" covers more than text that will not parse. A save also holds numbers, and a number
/// that parses is not automatically a number the game can be resumed at — see
/// <see cref="Deserialize"/> for the position, which is the one the game cannot survive.
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
    /// this build can read, or holds a position the game cannot be resumed at.
    /// </returns>
    /// <remarks>
    /// The position is checked here rather than left to the caller because it is the one field a
    /// bad value in cannot be shrugged off later: it is read once, at the moment the game is
    /// resumed, and from then on it is simply where the player is.
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
            if (save is null || !CanBeResumedAt(save.PlayerX) || !CanBeResumedAt(save.PlayerY))
            {
                return null;
            }

            // a save written as JSON null, or with a null quest list, is still readable
            return save with { Quests = save.Quests ?? [] };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a saved coordinate is one a game can actually be resumed at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world model measures in <see cref="double"/> and is unbounded, so there is no world
    /// size to check against and none is invented here — a player who flew a very long way must
    /// still be able to load. What there is, is a narrower type further down: the position is
    /// handed to the drawing side as a <see cref="float"/>, and a camera refuses a target that is
    /// not finite. So a coordinate beyond the <see cref="float"/> range is not a distant position,
    /// it is a position that becomes infinity on the way to the screen.
    /// </para>
    /// <para>
    /// Left unchecked it used to draw the whole world at nowhere, which shows as a blank screen;
    /// now that the camera refuses it, it would instead throw once every frame the game tried to
    /// draw. Both are worse than the file being treated as damaged, which is what it is.
    /// </para>
    /// </remarks>
    /// <param name="coordinate">The saved coordinate, on either axis.</param>
    /// <returns><c>true</c> when the game can be resumed at it.</returns>
    static bool CanBeResumedAt(double coordinate) => float.IsFinite((float)coordinate);
}
