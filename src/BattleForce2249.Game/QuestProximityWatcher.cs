using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Watches how close the player is to each quest's markers and drives the quest API accordingly.
/// </summary>
/// <remarks>
/// This is the half of a proximity quest that Pilgrimage deliberately does not own. The quest
/// model declares the rule — a <see cref="QuestTriggerKind.Proximity"/> trigger and a distance —
/// and this measures against the world's markers and calls <see cref="Quest.Start"/> or
/// <see cref="Quest.Complete"/> when it is satisfied. Both are safe to call every frame, so the
/// watcher keeps no memory of what it has already fired.
///
/// It keeps no memory of where the player was, either. A frame is measured as the ground the
/// player covered during it, and the caller passes both ends of that — the alternative, having
/// the watcher remember last frame's position, would make it the only thing here holding state
/// across frames and would quietly go wrong the moment a save is resumed or the player is placed
/// somewhere by anything other than flying there.
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for a player standing still at the given position.
    /// </summary>
    /// <remarks>
    /// A standstill is a frame that covered no ground, so this is the swept overload with both
    /// ends in the same place. Prefer <see cref="Update(QuestLog, Position, Position)"/> from the
    /// frame loop: a player who moved and is measured as a point is exactly the trigger a fast
    /// ship flies through.
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is.</param>
    public void Update(QuestLog quests, Position playerPosition) =>
        Update(quests, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers against the ground the player covered this frame. Called once
    /// per frame.
    /// </summary>
    /// <remarks>
    /// The whole frame counts, not the instant it ended. Measuring only where the player finished
    /// means a trigger fires solely because a frame happened to land inside it, and a frame long
    /// enough to carry the ship from one side of a marker to the other steps clean over it —
    /// pillar 1 calls that a bug rather than something to tune the markers around.
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="from">Where the player was when the frame began.</param>
    /// <param name="to">Where the player is now the frame has ended.</param>
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

            if (quest.State is QuestState.NotStarted
                && definition.AutoStarts
                && HasFired(definition.Start, from, to, markers.Start))
            {
                quest.Start();
            }

            // checked after the start above, so arriving already on the end marker still finishes,
            // and so does a frame long enough to cross both markers at once
            if (quest.State is QuestState.Active
                && HasFired(definition.End, from, to, markers.End))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close the player's travel this frame took
    /// them to its marker.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position from, Position to, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && marker.DistanceToSegment(from, to) <= trigger.Distance;
}
