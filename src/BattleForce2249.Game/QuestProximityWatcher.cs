using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Watches how close the player is to each quest's markers and drives the quest API accordingly.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of a proximity quest that Pilgrimage deliberately does not own. The quest
/// model declares the rule — a <see cref="QuestTriggerKind.Proximity"/> trigger and a distance —
/// and this measures against the world's markers and calls <see cref="Quest.Start"/> or
/// <see cref="Quest.Complete"/> when it is satisfied. Both are safe to call every frame, so the
/// watcher keeps no memory of what it has already fired.
/// </para>
/// <para>
/// It keeps no memory of where the player was, either. Markers are measured against the ground the
/// player covered since the last frame, and the caller supplies both ends of that — the player
/// knows where it travelled from, and a watcher remembering it instead would sweep a line nobody
/// flew the first frame after a game is resumed.
/// </para>
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for a player who has not moved. The standstill case of
    /// <see cref="Update(QuestLog, Position, Position)"/>: a journey with both ends in the same
    /// place is a point.
    /// </summary>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(QuestLog quests, Position playerPosition) =>
        Update(quests, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers against the ground the player covered this frame. Called once
    /// per frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measuring the journey rather than the point it ended at is what stops a marker being stepped
    /// over: a frame long enough to carry the player from outside one side of a trigger to outside
    /// the other used to fire nothing at all, which made trigger distances the only thing standing
    /// between a fast ship and a quest that silently never starts.
    /// </para>
    /// <para>
    /// The two triggers are ordered against each other along the journey, so a quest cannot be
    /// completed at a point the player reached <em>before</em> it started. Without that, one frame
    /// flying a quest's ground backwards — past the exit marker and on to the start marker — would
    /// both begin and finish the quest, handing the player a completed quest for arriving at its
    /// beginning. A quest already under way when the frame opened has no such constraint; it was
    /// started before any of this ground was covered.
    /// </para>
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="from">Where the player was when the frame opened.</param>
    /// <param name="to">Where the player is now.</param>
    public void Update(QuestLog quests, Position from, Position to)
    {
        foreach (QuestMarkers markers in world.QuestMarkers)
        {
            Quest? quest = quests.Find(markers.QuestId);
            if (quest is null)
            {
                // markers for a quest this campaign no longer ships; nothing to drive
                continue;
            }

            QuestDefinition definition = quest.Definition;

            // how far along this frame's journey the quest began, when it began on this frame
            double? startedAt = null;

            if (quest.State is QuestState.NotStarted
                && definition.AutoStarts
                && FiredAt(definition.Start, markers.Start, from, to) is double start)
            {
                quest.Start();
                startedAt = start;
            }

            // checked after the start above, so arriving already on the end marker still finishes
            if (quest.State is QuestState.Active
                && FiredAt(definition.End, markers.End, from, to) is double end
                && end >= (startedAt ?? 0))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Finds how far along the journey a trigger was satisfied, or <c>null</c> if it was not.
    /// </summary>
    static double? FiredAt(QuestTrigger trigger, Position marker, Position from, Position to) =>
        trigger.Kind is QuestTriggerKind.Proximity
            ? marker.FirstApproachWithin(from, to, trigger.Distance)
            : null;
}
