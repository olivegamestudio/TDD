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
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for where the player is now. Called once per frame.
    /// </summary>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(QuestLog quests, Position playerPosition)
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
                && HasFired(definition.Start, playerPosition, markers.Start))
            {
                quest.Start();
            }

            // checked after the start above, so arriving already on the end marker still finishes
            if (quest.State is QuestState.Active
                && HasFired(definition.End, playerPosition, markers.End))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by the player's distance from its marker.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position player, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && player.DistanceTo(marker) <= trigger.Distance;
}
