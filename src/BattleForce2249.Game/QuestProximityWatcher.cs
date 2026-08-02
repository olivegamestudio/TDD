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
/// watcher keeps no memory of what it has already fired — and it keeps none of where the player
/// was either, because the caller has both ends of a frame's travel and can simply pass them.
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for a player who has not moved.
    /// </summary>
    /// <remarks>
    /// A journey of no length, and it behaves exactly as measuring the point does, because a
    /// segment whose ends coincide is a point.
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
    /// <para>
    /// Sweeping a journey rather than sampling its end means both of a quest's markers can be
    /// passed within one frame, so the markers are applied <em>in the order they were reached</em>
    /// and not merely both. A frame long enough to fly the whole field forwards plays the quest
    /// through, which is right; the same frame flown backwards would otherwise finish a quest the
    /// player only began on the way out of it, which is not.
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

            // how far into this frame's journey the quest was already running. A quest active
            // before the frame opened has been running for the whole of it, so its end marker
            // counts wherever along the journey it was passed.
            double runningFrom = 0;

            if (quest.State is QuestState.NotStarted && definition.AutoStarts)
            {
                ClosestApproach start = markers.Start.ClosestApproachTo(from, to);
                if (HasFired(definition.Start, start))
                {
                    quest.Start();
                    runningFrom = start.Along;
                }
            }

            // checked after the start above, so arriving already on the end marker still finishes —
            // which now includes a single frame long enough to sweep past both of a quest's markers
            if (quest.State is QuestState.Active)
            {
                ClosestApproach end = markers.End.ClosestApproachTo(from, to);

                // the end marker has to have been reached no earlier in the journey than the start
                // marker was, so a quest cannot be finished by a leg flown before it began
                if (HasFired(definition.End, end) && end.Along >= runningFrom)
                {
                    quest.Complete();
                }
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close the player's journey came to its
    /// marker, at its closest.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, ClosestApproach approach) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && approach.Distance <= trigger.Distance;
}
