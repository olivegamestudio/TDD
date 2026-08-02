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
/// <remarks>
/// It keeps no memory of where the player was, either. A trigger is measured against the whole
/// journey a frame covered, and the caller is told both ends because the caller is the only thing
/// that knows them. Remembering last frame's position here would look equivalent and go wrong the
/// moment the player is <em>placed</em> rather than flown — entering the screen, or resuming a
/// save — because the next frame would then sweep a phantom journey from wherever the last game
/// left off and fire every trigger along a line nobody travelled.
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for a player standing still at the given position.
    /// </summary>
    /// <remarks>
    /// A journey that went nowhere, which is why this is the same rule as
    /// <see cref="Update(QuestLog, Position, Position)"/> rather than a second one beside it.
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is.</param>
    public void Update(QuestLog quests, Position playerPosition) =>
        Update(quests, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers for the ground the player covered this frame. Called once
    /// per frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The markers are measured against the <em>segment</em> from <paramref name="from"/> to
    /// <paramref name="to"/>, not against <paramref name="to"/> alone. Sampling the end of a frame
    /// fires a trigger only when a frame happens to land inside it, so a ship travelling at speed
    /// flies straight through triggers it passed within metres of — and the only thing standing
    /// between the game and that today is how generously the content authored its distances.
    /// Pillar 1 in <c>docs/DESIGN.md</c> calls that a defect rather than a tuning detail.
    /// </para>
    /// <para>
    /// A frame is the smallest unit of time the game has, so the moments inside one are not
    /// ordered: a single frame long enough to cover a quest's whole field starts <em>and</em>
    /// completes it, in either direction of travel. That is a quest played in one frame rather
    /// than a quest skipped, and it takes a stall of several seconds to reach. Ordering the
    /// moments within a frame would mean a trigger that depends on which way the ship was
    /// pointing, and <see cref="QuestDefinition"/> makes no claim about direction.
    /// </para>
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="from">Where the player was when the frame opened.</param>
    /// <param name="to">Where the player is now the frame has moved them.</param>
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

            // checked after the start above, so arriving already on the end marker still finishes —
            // and so one long frame across the whole field plays the quest rather than skipping it
            if (quest.State is QuestState.Active
                && HasFired(definition.End, from, to, markers.End))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close its marker came to the ground the
    /// player covered this frame.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position from, Position to, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && marker.DistanceToSegment(from, to) <= trigger.Distance;
}
