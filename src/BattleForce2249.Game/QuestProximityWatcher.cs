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
    /// Applies each quest's triggers for a player who has not moved.
    /// </summary>
    /// <remarks>
    /// A zero length journey, and it behaves exactly as measuring the point does, because a segment
    /// whose ends coincide is a point.
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is.</param>
    public void Update(QuestLog quests, Position playerPosition) =>
        Update(quests, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers for the ground the player covered this frame. Called once per
    /// frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured against the whole journey rather than its destination. Sampling the end point alone
    /// fires a trigger only when a frame happens to <em>land</em> inside it, so a frame that carried
    /// the player from just outside one side of a marker to just outside the other never fires it —
    /// the ship flies through a trigger it passed within metres of. Pillar 1 calls that a bug rather
    /// than a tuning detail, and the only thing standing between the game and it was how generous
    /// the authored distances happened to be.
    /// </para>
    /// <para>
    /// The watcher still remembers nothing. Both ends of the journey come from the caller, which
    /// has them both within a single frame, so no state is kept here or anywhere else — and a
    /// player who was placed rather than flown, on entering the screen or resuming a save, does not
    /// drag a phantom journey behind them from wherever the last one ended.
    /// </para>
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="from">Where the player was when the frame began.</param>
    /// <param name="to">Where the frame's travel left them.</param>
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
            // which now includes a single frame long enough to sweep past both of a quest's markers
            if (quest.State is QuestState.Active
                && HasFired(definition.End, from, to, markers.End))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close the player's journey came to its
    /// marker, at its closest.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position from, Position to, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && marker.DistanceToSegment(from, to) <= trigger.Distance;
}
